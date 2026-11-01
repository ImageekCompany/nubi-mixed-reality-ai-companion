using System.IO;
using System.Linq;
using UnityEngine;

namespace Jolly.Tests.EditMode.Asr
{
    static class AsrFixtures
    {
        public static string[] Lines(string name) =>
            File.ReadAllLines(Path(name)).Where(line => line.Length > 0).ToArray();

        public static string Text(string name) => File.ReadAllText(Path(name)).Trim();

        static string Path(string name) => System.IO.Path.Combine(Application.dataPath, "Jolly/Tests/Fixtures/asr", name);
    }
}
