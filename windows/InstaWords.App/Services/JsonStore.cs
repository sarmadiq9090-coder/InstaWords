using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace InstaWords.App.Services
{
    /// <summary>Reads and writes [DataContract] objects as indented UTF-8 JSON (no third-party libraries).</summary>
    public static class JsonStore
    {
        public static T Load<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (var fs = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(T));
                    return ser.ReadObject(fs) as T;
                }
            }
            catch (Exception ex)
            {
                // A damaged file must not stop the app: keep a copy for diagnosis and start fresh.
                Log.Warn($"Could not read {path}: {ex.Message}");
                try { File.Copy(path, path + ".bad", overwrite: true); } catch { /* ignore */ }
                return null;
            }
        }

        public static string Serialize<T>(T value)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, new UTF8Encoding(false), ownsStream: false, indent: true, indentChars: "  "))
                {
                    new DataContractJsonSerializer(typeof(T)).WriteObject(writer, value);
                    writer.Flush();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>Atomic save: write a temp file, then swap it in.</summary>
        public static bool Save<T>(string path, T value)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var json = Serialize(value);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(tmp, path, null, ignoreMetadataErrors: true);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not save {path}: {ex.Message}");
                return false;
            }
        }
    }

    internal static class Log
    {
        public static void Warn(string message) => System.Diagnostics.Trace.TraceWarning("[InstaWords] " + message);
    }
}
