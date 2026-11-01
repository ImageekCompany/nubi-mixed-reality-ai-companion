using System.IO;

namespace Jolly.Core.Diagnostics
{
    /// <summary>
    /// Crash-safe whole-file writes for every store: write <c>path.tmp</c>, then swap it in. <c>File.Replace</c> throws
    /// <see cref="FileNotFoundException"/> when the destination is missing, so the first save uses <c>File.Move</c>.
    /// </summary>
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string text)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }

        public static bool TryReadAllText(string path, out string text)
        {
            text = File.Exists(path) ? File.ReadAllText(path) : null;
            return text != null;
        }

        public static void Delete(string path)
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }
}
