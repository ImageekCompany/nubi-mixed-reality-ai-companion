using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// Records each scripted turn scenario to its own Profiler binary log (<c>selftest_turns_&lt;scenario&gt;.raw</c>) with
    /// managed allocation call stacks, for <c>Jolly.Editor.Tools.GcAllocReport</c> (docs/perf/README.md §7). Armed by the
    /// <see cref="FlagFile"/> next to <c>selftest.json</c> (<c>selftest.sh --profile</c>) and only in development players,
    /// the only ones with a Profiler. The capture adds its own overhead, so its runs give attribution, not budgets.
    /// </summary>
    public sealed class TurnProfileCapture
    {
        public const string FlagFile = "selftest_profile";
        /// <summary>Prefix of the logs; Unity appends <c>.raw</c> to <see cref="Profiler.logFile"/>.</summary>
        public const string LogPrefix = "selftest_turns_";

        readonly string _dataRoot;
        bool _recording;

        TurnProfileCapture(string dataRoot) => _dataRoot = dataRoot;

        /// <summary>Null unless the flag was present (it is consumed) and the player can profile.</summary>
        public static TurnProfileCapture TryArm(string dataRoot)
        {
            var flag = Path.Combine(dataRoot, FlagFile);
            if (!File.Exists(flag))
                return null;
            File.Delete(flag);
            if (!Profiler.supported || Application.isEditor)
                return null;
            foreach (var old in Directory.GetFiles(dataRoot, LogPrefix + "*.raw"))
                File.Delete(old);
            return new TurnProfileCapture(dataRoot);
        }

        public void Begin(string scenario)
        {
            if (_recording)
                return;
            _recording = true;
            Profiler.logFile = Path.Combine(_dataRoot, LogPrefix + scenario);
            Profiler.enableAllocationCallstacks = true;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
        }

        public void End()
        {
            if (!_recording)
                return;
            _recording = false;
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.enableAllocationCallstacks = false;
            Profiler.logFile = string.Empty;
        }
    }
}
