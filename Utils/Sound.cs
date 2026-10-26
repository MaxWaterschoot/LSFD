using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Rage;

namespace LSFD.Utils
{
    /// <summary>A sound started with <see cref="Sound"/>: one file or several back to back, once or looped.</summary>
    internal sealed class SoundHandle
    {
        internal readonly Queue<string> Files;
        internal readonly bool Loop;
        internal volatile int Volume;
        internal volatile bool StopRequested;
        internal string Alias;
        internal int StartedAt;
        internal int SentVolume;

        /// <summary>True once it played to the end or was stopped.</summary>
        public bool IsFinished { get; internal set; }

        internal SoundHandle(IEnumerable<string> files, bool loop, float volume)
        {
            Files = new Queue<string>(files);
            Loop = loop;
            Volume = Sound.ToMci(volume);
        }

        public void Stop() => StopRequested = true;

        /// <param name="volume">0 to 1.</param>
        public void SetVolume(float volume) => Volume = Sound.ToMci(volume);
    }

    /// <summary>A looped sound that gets quieter the further the player is from it, like an alarm in a building.</summary>
    internal sealed class PositionalSound
    {
        private readonly SoundHandle handle;
        private readonly float volume;
        private readonly float range;

        public Vector3 Position { get; set; }

        internal PositionalSound(SoundHandle handle, Vector3 position, float volume, float range)
        {
            this.handle = handle;
            this.volume = volume;
            this.range = range;
            Position = position;
            Update();
        }

        public bool IsFinished => handle.IsFinished;

        public void Stop() => handle.Stop();

        internal void Update()
        {
            float distance = Game.LocalPlayer.Character.DistanceTo(Position);
            float fall = Math.Max(0f, 1f - distance / range);
            handle.SetVolume(volume * fall * fall);
        }
    }

    /// <summary>
    /// Plays LSFD's WAV files from Plugins/LSFD/Audio/ through Windows MCI, on a thread of its own so the game never
    /// waits for it. Unlike SoundPlayer, several sounds can play at once: an alarm keeps sounding while dispatch talks.
    /// Radio transmissions are queued and play one after the other.
    /// </summary>
    internal static class Sound
    {
        private const int TickMilliseconds = 15;

        // MCI reports a clip as stopped for a moment while it starts.
        private const int MinClipTime = 80;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

        private static readonly ConcurrentQueue<SoundHandle> started = new ConcurrentQueue<SoundHandle>();
        private static readonly ConcurrentQueue<SoundHandle> radioQueue = new ConcurrentQueue<SoundHandle>();
        private static readonly List<PositionalSound> positional = new List<PositionalSound>();
        private static readonly HashSet<string> reportedMissing = new HashSet<string>();

        // Rage's log is written from the game thread, so the audio thread leaves its messages here.
        private static readonly ConcurrentQueue<string> log = new ConcurrentQueue<string>();

        private static Thread thread;
        private static volatile bool running;
        private static int nextAlias;

        /// <summary>Whether an optional file is there, without logging it as missing.</summary>
        public static bool Exists(string relativePath) => File.Exists(Path.Combine(ModFolder.Audio, relativePath));

        /// <summary>The full path of a file under Plugins/LSFD/Audio/, or null when it isn't there.</summary>
        public static string Find(string relativePath)
        {
            string path = Path.GetFullPath(Path.Combine(ModFolder.Audio, relativePath));
            if (File.Exists(path))
                return path;

            lock (reportedMissing)
            {
                if (reportedMissing.Add(relativePath))
                    Game.LogTrivial($"LSFD: sound {relativePath} not found in {ModFolder.Audio}.");
            }
            return null;
        }

        /// <summary>Plays files under Plugins/LSFD/Audio/ back to back at a volume of 0 to 1. Missing files are skipped; null when none exist.</summary>
        public static SoundHandle Play(float volume, params string[] relativePaths) => Start(relativePaths, false, volume, false);

        public static SoundHandle PlayLooped(string relativePath, float volume) => Start(new[] { relativePath }, true, volume, false);

        /// <summary>
        /// A transmission on the radio: waits for the one before it, with the mic clicks around it. A <c>tone</c> plays
        /// before the mic opens.
        /// </summary>
        public static SoundHandle Radio(float volume, IEnumerable<string> relativePaths, string tone = null)
        {
            // Missing fragments are left out; with nothing left to say the mic isn't keyed at all.
            string[] content = relativePaths.Where(p => Find(p) != null).ToArray();
            if (content.Length == 0)
                return null;

            var files = new List<string>();
            if (tone != null)
                files.Add(tone);
            files.Add("Radio/MIC_CLICK_ON.wav");
            files.AddRange(content);
            files.Add("Radio/MIC_CLICK_OFF.wav");
            return Start(files, false, volume, true);
        }

        /// <summary>A looped sound at <paramref name="position"/>, heard up to <paramref name="range"/> meters away.</summary>
        public static PositionalSound PlayAt(string relativePath, Vector3 position, float volume, float range = 40f)
        {
            SoundHandle handle = Start(new[] { relativePath }, true, 0f, false);
            if (handle == null)
                return null;

            var sound = new PositionalSound(handle, position, volume, range);
            positional.Add(sound);
            return sound;
        }

        /// <summary>Updates the volume of positional sounds; called every tick.</summary>
        public static void Process()
        {
            while (log.TryDequeue(out string message))
                Game.LogTrivial(message);

            positional.RemoveAll(s => s.IsFinished);
            foreach (PositionalSound sound in positional)
                sound.Update();
        }

        public static void Shutdown()
        {
            running = false;
            thread?.Join(1000);
            thread = null;
            positional.Clear();
        }

        internal static int ToMci(float volume) => (int)(Math.Max(0f, Math.Min(1f, volume)) * 1000f);

        private static SoundHandle Start(IEnumerable<string> relativePaths, bool loop, float volume, bool radio)
        {
            string[] files = relativePaths.Select(Find).Where(f => f != null).ToArray();
            if (files.Length == 0)
                return null;

            EnsureThread();
            var handle = new SoundHandle(files, loop, volume);
            (radio ? radioQueue : started).Enqueue(handle);
            return handle;
        }

        private static void EnsureThread()
        {
            if (thread != null)
                return;

            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "LSFD audio" };
            thread.Start();
        }

        // Every MCI command is sent from this one thread.
        private static void Run()
        {
            var playing = new List<SoundHandle>();
            SoundHandle radio = null;

            try
            {
                while (running)
                {
                    while (started.TryDequeue(out SoundHandle handle))
                        playing.Add(handle);

                    if (radio == null && radioQueue.TryDequeue(out SoundHandle next))
                        radio = next;
                    if (radio != null && !Advance(radio))
                        radio = null;

                    playing.RemoveAll(handle => !Advance(handle));
                    Thread.Sleep(TickMilliseconds);
                }
            }
            catch (Exception e)
            {
                log.Enqueue($"LSFD: the audio thread stopped: {e.Message}");
            }
            finally
            {
                Send("close all");
            }
        }

        // Starts the next file when the current one is done; returns false once the handle is finished.
        private static bool Advance(SoundHandle handle)
        {
            if (handle.StopRequested)
            {
                Close(handle);
                handle.IsFinished = true;
                return false;
            }

            if (handle.Alias != null)
            {
                SendVolume(handle);
                if (handle.Loop || Environment.TickCount - handle.StartedAt < MinClipTime || Query($"status {handle.Alias} mode") == "playing")
                    return true;
                Close(handle);
            }

            while (handle.Files.Count > 0)
            {
                string alias = "lsfd" + Interlocked.Increment(ref nextAlias);
                string file = handle.Files.Dequeue();
                if (Send($"open \"{file}\" type mpegvideo alias {alias}") != 0)
                {
                    log.Enqueue($"LSFD: could not open {file}.");
                    continue;
                }

                handle.Alias = alias;
                handle.StartedAt = Environment.TickCount;
                handle.SentVolume = -1;
                SendVolume(handle);
                Send(handle.Loop ? $"play {alias} repeat" : $"play {alias}");
                return true;
            }

            handle.IsFinished = true;
            return false;
        }

        private static void Close(SoundHandle handle)
        {
            if (handle.Alias == null)
                return;
            Send($"stop {handle.Alias}");
            Send($"close {handle.Alias}");
            handle.Alias = null;
        }

        private static void SendVolume(SoundHandle handle)
        {
            int volume = handle.Volume;
            if (volume == handle.SentVolume)
                return;
            Send($"setaudio {handle.Alias} volume to {volume}");
            handle.SentVolume = volume;
        }

        private static int Send(string command) => mciSendString(command, null, 0, IntPtr.Zero);

        private static string Query(string command)
        {
            var result = new StringBuilder(64);
            return mciSendString(command, result, result.Capacity, IntPtr.Zero) == 0 ? result.ToString() : "";
        }
    }
}
