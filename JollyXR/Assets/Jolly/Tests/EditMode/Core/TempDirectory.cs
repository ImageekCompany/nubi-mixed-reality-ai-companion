using System;
using System.IO;

namespace Jolly.Tests.EditMode.Core
{
    static class TempDirectory
    {
        public static string Create()
        {
            var dir = Path.Combine(Path.GetTempPath(), "jolly-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void Delete(string dir)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
