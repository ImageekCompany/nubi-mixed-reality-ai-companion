using System.Collections.Generic;
using System.Globalization;
using Jolly.App.Adapters;
using Jolly.Character;
using Jolly.Core.Conversation;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// The verdict rules the turn scenarios share. A blown latency budget degrades a scenario and a failed key tool fails
    /// it: a release run passed a vision turn whose tool had failed, with the answer 8.3 s after the tool start, because
    /// budgets were only notes and a failed completion counted as completed (selftest-report.md, f6d8db3 run 3).
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
        /// (selftest-report.md, 6179cfc). Degrades, because the reply itself was fine.
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

        /// <summary>
        /// Who started come_here: "guard" (the local ComeGuard), "model", "guard+model" (the model restarted the walk:
        /// only a crisis turn still reaches the model after the guard walked, ADR-0037) or "none". <paramref name="callIds"/> is parallel to <paramref name="started"/>.
        /// </summary>
        public static string ComeHereSource(IReadOnlyList<string> started, IReadOnlyList<string> callIds)
        {
            bool guard = false, model = false;
            for (var i = 0; i < started.Count; i++)
            {
                if (started[i] != ToolNames.ComeHere)
                    continue;
                if (callIds[i] == TurnGuards.ComeHereCallId)
                    guard = true;
                else
                    model = true;
            }
            return guard ? model ? "guard+model" : "guard" : model ? "model" : "none";
        }

        public const string GuardSource = "guard";

        /// <summary>
        /// Who answered a turn (ADR-0047): "guard" (a local guard's body action and no model round), "guard+model" (the
        /// guard's action did not do what was asked, so the model explained it, and that turn has words to check), else
        /// "model". <paramref name="modelRounds"/> is the turn's <see cref="TurnObserver.Replies"/> count.
        /// </summary>
        public static string ActionSource(IReadOnlyList<string> callIds, int modelRounds)
        {
            bool guard = false, model = modelRounds > 0;
            foreach (var id in callIds)
            {
                if (id.StartsWith(TurnGuards.ActionCallPrefix, System.StringComparison.Ordinal))
                    guard = true;
                else
                    model = true;
            }
            return guard ? model ? "guard+model" : GuardSource : "model";
        }

        /// <summary>
        /// A short command the guard's tables hold must stay on the guard path; degrades, since the move still ran through
        /// the model.
        /// </summary>
        public static void GuardPath(ScenarioResult r, string source)
        {
            if (source != GuardSource)
                r.Degrade($"action_source={source}, not the guard");
        }

        /// <summary>
        /// The model itself must have called some action and it must have succeeded, for a phrase outside the guard's
        /// tables; which action is its choice. <paramref name="callIds"/> is parallel to <paramref name="started"/>.
        /// </summary>
        public static void ModelAction(ScenarioResult r, IReadOnlyList<string> started, IReadOnlyList<string> completed,
                                       IReadOnlyList<string> callIds)
        {
            var action = ModelActionCalled(started, callIds);
            if (r.Check(action != null, "the model called no action"))
                Tools(r, started, completed, action);
        }

        public static bool ModelCalledAction(IReadOnlyList<string> started, IReadOnlyList<string> callIds) =>
            ModelActionCalled(started, callIds) != null;

        static string ModelActionCalled(IReadOnlyList<string> started, IReadOnlyList<string> callIds)
        {
            for (var i = 0; i < started.Count; i++)
            {
                if (started[i].StartsWith(ToolNames.Actions.Namespace + ".", System.StringComparison.Ordinal)
                    && !callIds[i].StartsWith(TurnGuards.ActionCallPrefix, System.StringComparison.Ordinal))
                    return started[i];
            }
            return null;
        }

        /// <summary>The body may face this far off the user's head while speaking (ADR-0041), degrees.</summary>
        public const float MaxBodyYawOff = 25f;
        /// <summary>The head may face this far off the user's head while speaking, degrees.</summary>
        public const float MaxHeadYawOff = 15f;

        /// <summary>
        /// The character must face the user at the turn's first audio: past <see cref="MaxBodyYawOff"/> body or
        /// <see cref="MaxHeadYawOff"/> head degrades. A turn whose tool still points the look at an object, or one with
        /// <paramref name="check"/> false, is only recorded. <paramref name="sampled"/> is false when the turn logged no
        /// evt=look.user.
        /// </summary>
        public static void Facing(ScenarioResult r, bool sampled, LookTelemetry.Sample s, bool check = true)
        {
            if (!sampled)
            {
                r.Metric("look", "none");
                if (check)
                    r.Degrade("no evt=look.user at first audio");
                return;
            }
            r.Metric("look", Look(s.BodyYawOff, s.HeadYawOff) + " attending=" + s.Attending.ToString().ToLowerInvariant());
            if (check && s.Attending != AttentionTarget.Point)
                FacingWithin(r, s.BodyYawOff, s.HeadYawOff, "speaking");
        }

        /// <summary>Degrades past <see cref="MaxBodyYawOff"/> body or <see cref="MaxHeadYawOff"/> head.</summary>
        public static void FacingWithin(ScenarioResult r, float bodyYawOff, float headYawOff, string when)
        {
            if (bodyYawOff > MaxBodyYawOff)
                r.Degrade(string.Format(CultureInfo.InvariantCulture, "{0} with the body {1:0}° off the user (max {2:0}°)",
                    when, bodyYawOff, MaxBodyYawOff));
            if (headYawOff > MaxHeadYawOff)
                r.Degrade(string.Format(CultureInfo.InvariantCulture, "{0} with the head {1:0}° off the user (max {2:0}°)",
                    when, headYawOff, MaxHeadYawOff));
        }

        public static string Look(float bodyYawOff, float headYawOff) =>
            string.Format(CultureInfo.InvariantCulture, "body={0:0.0} head={1:0.0}", bodyYawOff, headYawOff);

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
