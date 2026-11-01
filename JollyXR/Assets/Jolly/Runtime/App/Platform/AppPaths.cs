using System.IO;
using UnityEngine;

namespace Jolly.App.Platform
{
    /// <summary>Where the app reads and writes its files (architecture §8, §11). Main thread only (Application paths).</summary>
    public sealed class AppPaths
    {
        public AppPaths(string dataRoot)
        {
            DataRoot = dataRoot;
            MemoryDirectory = Path.Combine(dataRoot, "memory");
        }

        public static AppPaths ForPlayer() => new(Application.persistentDataPath);

        public string DataRoot { get; }
        public string MemoryDirectory { get; private set; }
        public string SceneMemory => Path.Combine(MemoryDirectory, "scene.json");
        /// <summary>The self-test keeps its scripted turns out of the user's memory. Call before the graph is built.</summary>
        public void RedirectMemory(string directory) => MemoryDirectory = directory;

        public string Usage => Path.Combine(DataRoot, "usage.json");
        public string InstallId => Path.Combine(DataRoot, "install_id");
        public string DeviceSecrets => Path.Combine(DataRoot, "secrets.json");
        /// <summary>One character id, pushed with adb or written by Jolly/Character/Play As Selected Definition.</summary>
        public string CharacterOverride => Path.Combine(DataRoot, "character.txt");
        /// <summary>The repository's .env, two levels above Assets/ (Editor only).</summary>
        public static string EditorDotEnv => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".env"));
    }
}
