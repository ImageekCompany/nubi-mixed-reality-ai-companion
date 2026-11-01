using System.Collections.Generic;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// The verdict rules the turn scenarios share. A blown latency budget degrades a scenario and a failed key tool fails
    /// it: a release run passed a vision turn whose tool had failed, with the answer 8.3 s after the tool start, because
    /// budgets were only notes and a failed completion counted as completed (selftest-report.md, dd14847 run 3).
    /// </summary>
    public static class TurnChecks
    {
        /// <summary>DoD §5: release → first audio p50 ≤ 2.0 s.</summary>
        public const double FirstAudioBudgetMs = 2000;
        /// <summary>Twice the budget: past this the user has waited in silence, so it is a failure, not a slow turn.</summary>
        public const double MaxFirstAudioMs = 4000;
        /// <summary>DoD §5: tool start → answer audio p50 ≤ 2.5 s.</summary>
        public const double ToolToAnswerBudgetMs = 2500;

        /// <summary>NaN (no reply audio) fails.</summary>
        public static void FirstAudio(ScenarioResult r, double ms)
        {
            r.Check(ms <= MaxFirstAudioMs, "no reply audio within 4 s of release");
            if (ms > FirstAudioBudgetMs)
                r.Degrade("first audio over the 2.0 s budget");
        }

        /// <summary>
        /// The expected tool must be called and succeed at least once; any other failed tool, or a failed attempt of the
        /// expected one before a success, degrades. <paramref name="completed"/> holds names, failures with
        /// <see cref="TurnObserver.FailedSuffix"/>.
        /// </summary>
        public static void Tools(ScenarioResult r, IReadOnlyList<string> started, IReadOnlyList<string> completed,
                                 string expected)
        {
            foreach (var name in completed)
            {
                if (!name.EndsWith(TurnObserver.FailedSuffix, System.StringComparison.Ordinal))
                    continue;
                var tool = name.Substring(0, name.Length - TurnObserver.FailedSuffix.Length);
                if (tool != expected || Contains(completed, expected))
                    r.Degrade(tool + " failed");
            }
            if (expected == null)
                return;
            if (!r.Check(Contains(started, expected), "the model did not call " + expected))
                return;
            r.Check(Contains(completed, expected),
                Contains(completed, expected + TurnObserver.FailedSuffix) ? expected + " failed" : expected + " did not complete");
        }

        /// <summary>
        /// For a request another tool can also answer (find_object or capture_and_identify for "where is my mug?"):
        /// choosing another tool, or none, degrades with what the model called; a call of <paramref name="expected"/> is
        /// checked as in <see cref="Tools"/>. True when <paramref name="expected"/> was called.
        /// </summary>
        public static bool PreferredTool(ScenarioResult r, IReadOnlyList<string> started, IReadOnlyList<string> completed,
                                         string expected)
        {
            if (Contains(started, expected))
            {
                Tools(r, started, completed, expected);
                return true;
            }
            Tools(r, started, completed, null);
            r.Degrade($"the model called {(started.Count == 0 ? "no tool" : string.Join(",", started))} instead of {expected}");
            return false;
        }

        /// <summary>
        /// A turn that asked for no movement must not walk: the greeting called come_here in 4 of 7 release runs
        /// (selftest-report.md, 4d228c8). Degrades, because the reply itself was fine.
        /// </summary>
        public static void StayedPut(ScenarioResult r, IReadOnlyList<string> started)
        {
            foreach (var name in started)
            {
                if (name is ToolNames.ComeHere or ToolNames.WalkTo or ToolNames.GoToSurface or ToolNames.FollowUser)
                {
                    r.Degrade($"moved without being asked ({name})");
                    return;
                }
            }
        }

        /// <summary>NaN (no answer after the tool) fails.</summary>
        public static void ToolToAnswer(ScenarioResult r, double ms)
        {
            if (!r.Check(!double.IsNaN(ms), "no answer audio after the tool"))
                return;
            if (ms > ToolToAnswerBudgetMs)
                r.Degrade("tool start → answer audio over the 2.5 s budget");
        }

        static bool Contains(IReadOnlyList<string> names, string name)
        {
            foreach (var n in names)
            {
                if (n == name)
                    return true;
            }
            return false;
        }
    }
}
