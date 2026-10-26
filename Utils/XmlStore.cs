using System;
using System.IO;
using System.Xml.Serialization;
using Rage;

namespace LSFD.Utils
{
    /// <summary>Reads and writes the plugin's XML data files, creating them with defaults when missing.</summary>
    internal static class XmlStore
    {
        public static readonly string DataFolder = ModFolder.Data;

        /// <summary>Loads <paramref name="fileName"/> from <paramref name="folder"/> (the Data folder when null), creating it from <paramref name="createDefault"/> when missing.</summary>
        public static T Load<T>(string fileName, Func<T> createDefault, string folder = null)
        {
            string path = Path.Combine(folder ?? DataFolder, fileName);

            if (!File.Exists(path))
            {
                T defaults = createDefault();
                Save(fileName, defaults, folder);
                Game.LogTrivial($"Created {path} with default values.");
                return defaults;
            }

            try
            {
                using (var stream = File.OpenRead(path))
                    return (T)new XmlSerializer(typeof(T)).Deserialize(stream);
            }
            catch (Exception e)
            {
                Game.LogTrivial($"Could not read {path}, using default values: {e.Message}");
                return createDefault();
            }
        }

        public static void Save<T>(string fileName, T value, string folder = null)
        {
            folder = folder ?? DataFolder;
            Directory.CreateDirectory(folder);
            using (var stream = File.Create(Path.Combine(folder, fileName)))
                new XmlSerializer(typeof(T)).Serialize(stream, value);
        }
    }
}
