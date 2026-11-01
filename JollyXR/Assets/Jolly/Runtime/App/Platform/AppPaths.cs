using System.IO;
using UnityEngine;

namespace Jolly.App.Platform
{
    /// <summary>Where the app reads and writes its files (architecture §8, §11). Main thread only (Application paths).</summary>
    public sealed class AppPaths
    {
        public AppPaths(string dataRoot) => DataRoot = dataRoot;

        public static AppPaths ForPlayer() => new(Application.persistentDataPath);

        public string DataRoot { get; }
        public string MemoryDirectory => Path.Combine(DataRoot, "memory");
        public string SceneMemory => Path.Combine(MemoryDirectory, "scene.json");
        public string Usage => Path.Combine(DataRoot, "usage.json");
        public string InstallId => Path.Combine(DataRoot, "install_id");
        public string DeviceSecrets => Path.Combine(DataRoot, "secrets.json");
        /// <summary>The repository's .env, two levels above Assets/ (Editor only).</summary>
        public static string EditorDotEnv => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".env"));
    }
}
