"""Generates the LSFD dispatch voice fragments with Piper TTS or ElevenLabs and gives them a radio effect.

Usage:
    Piper (free, offline):
        pip install piper-tts        (and ffmpeg on your PATH)
        python generate_voices.py --voice path/to/en-us-ryan-high.onnx --csv dispatch_fragments.csv \
            --out ../../Plugins/LSFD/Audio/Dispatch

    ElevenLabs (API key needed, no pip packages):
        set ELEVENLABS_API_KEY=...   (macOS/Linux: export ELEVENLABS_API_KEY=...)
        python generate_voices.py --engine elevenlabs --voice <voice id> --csv dispatch_fragments.csv \
            --out ../../Plugins/LSFD/Audio/Dispatch --force

Writes every row of the CSV to <out>/<file> with the radio effect, and the clean TTS output to <raw>/<file>
(keep those out of Plugins/, they are only there to re-run the filter without generating again).
Only rows whose output file doesn't exist yet are generated, unless --force is given.
Use --only to (re)generate a few rows, e.g. --only Units/ENGINE.wav Units/LADDER.wav
"""
import argparse, csv, json, os, subprocess, sys, urllib.error, urllib.request, wave

# Portable radio: telephone band, a bit of compression and grit, then normalized.
TRIM = ("silenceremove=start_periods=1:start_threshold=-45dB,areverse,"
        "silenceremove=start_periods=1:start_threshold=-45dB,areverse,apad=pad_dur=0.04,")
RADIO_FILTER = TRIM + ("highpass=f=300,lowpass=f=3400,acompressor=threshold=0.1:ratio=6:attack=5:release=50,"
                "acrusher=bits=10:mix=0.25,volume=1.6,alimiter=limit=0.95")

ELEVENLABS_URL = "https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=mp3_44100_128"


class PiperEngine:
    ext = ".wav"

    def __init__(self, a):
        from piper import PiperVoice, SynthesisConfig
        self.voice = PiperVoice.load(a.voice)
        self.config = SynthesisConfig(length_scale=a.speed)

    def synthesize(self, text, raw_file):
        with wave.open(raw_file, "wb") as wf:
            self.voice.synthesize_wav(text, wf, syn_config=self.config)


class ElevenLabsEngine:
    ext = ".mp3"

    def __init__(self, a):
        self.key = os.environ.get("ELEVENLABS_API_KEY")
        if not self.key:
            sys.exit("Set the ELEVENLABS_API_KEY environment variable first.")
        self.voice = a.voice
        self.model = a.model
        # The same settings for every fragment, so they all sound like one dispatcher.
        self.settings = {"stability": a.stability, "similarity_boost": 0.8, "style": 0.0,
                         "use_speaker_boost": True, "speed": a.el_speed}
        self.seed = a.seed

    def synthesize(self, text, raw_file):
        body = {"text": text, "model_id": self.model, "voice_settings": self.settings, "seed": self.seed}
        req = urllib.request.Request(ELEVENLABS_URL.format(voice=self.voice), data=json.dumps(body).encode(),
                                     headers={"xi-api-key": self.key, "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=60) as r, open(raw_file, "wb") as f:
                f.write(r.read())
        except urllib.error.HTTPError as e:
            sys.exit(f"ElevenLabs refused '{text}': {e.code} {e.read().decode(errors='replace')}")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--engine", choices=["piper", "elevenlabs"], default="piper")
    p.add_argument("--voice", required=True, help="Piper: path to the .onnx. ElevenLabs: the voice ID")
    p.add_argument("--csv", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--raw", default="raw/Dispatch", help="where the clean, unfiltered TTS output goes")
    p.add_argument("--speed", type=float, default=0.92, help="Piper: < 1 speaks faster, like a dispatcher")
    p.add_argument("--model", default="eleven_multilingual_v2", help="ElevenLabs model ID")
    p.add_argument("--stability", type=float, default=0.6, help="ElevenLabs: higher = more monotone, like dispatch")
    p.add_argument("--el-speed", type=float, default=1.08, help="ElevenLabs: 0.7 to 1.2")
    p.add_argument("--seed", type=int, default=1, help="ElevenLabs: fixed so a re-run sounds the same")
    p.add_argument("--only", nargs="*", help="only these files (the 'file' column)")
    p.add_argument("--force", action="store_true")
    a = p.parse_args()

    engine = PiperEngine(a) if a.engine == "piper" else ElevenLabsEngine(a)

    with open(a.csv, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    if a.only:
        rows = [r for r in rows if r["file"] in a.only]

    for row in rows:
        out_file = os.path.join(a.out, row["file"])
        raw_file = os.path.splitext(os.path.join(a.raw, row["file"]))[0] + engine.ext
        if os.path.exists(out_file) and not a.force:
            continue
        os.makedirs(os.path.dirname(out_file), exist_ok=True)
        os.makedirs(os.path.dirname(raw_file), exist_ok=True)

        engine.synthesize(row["text"], raw_file)

        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", raw_file, "-af", RADIO_FILTER,
                        "-ar", "44100", "-ac", "1", "-sample_fmt", "s16", out_file], check=True)
        print(f"{row['file']:45} {row['text']}")

if __name__ == "__main__":
    main()
