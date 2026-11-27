using System.IO;

namespace Relead.CrossPromo
{
    /// <summary>
    /// Replace a file in one rename, so a crash or full disk leaves either the old or the new copy, never none
    /// (the old copy is only gone once the new one is in place).
    /// </summary>
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string text)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            Replace(tmp, path);
        }

        /// <summary>Moves <paramref name="source"/> over <paramref name="destination"/> (same directory).</summary>
        public static void Replace(string source, string destination)
        {
            if (File.Exists(destination)) File.Replace(source, destination, null, true);
            else File.Move(source, destination);
        }
    }
}
