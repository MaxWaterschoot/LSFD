using System;
using System.IO;
using System.Xml.Serialization;
using Rage;

namespace LSFD.Utils
{
    /// <summary>Reads and writes the plugin's XML data files, creating them with defaults when missing.</summary>
    internal static class XmlStore
    {
        public const string DataFolder = "Plugins/LSFD";

        public static T Load<T>(string fileName, Func<T> createDefault)
        {
            string path = Path.Combine(DataFolder, fileName);

            if (!File.Exists(path))
            {
                T defaults = createDefault();
                Save(fileName, defaults);
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

        public static void Save<T>(string fileName, T value)
        {
            Directory.CreateDirectory(DataFolder);
            using (var stream = File.Create(Path.Combine(DataFolder, fileName)))
                new XmlSerializer(typeof(T)).Serialize(stream, value);
        }
    }
}
