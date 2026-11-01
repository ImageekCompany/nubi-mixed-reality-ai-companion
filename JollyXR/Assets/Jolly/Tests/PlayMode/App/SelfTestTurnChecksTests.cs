using System.Collections.Generic;
using Jolly.App.SelfTest;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.App
{
    public sealed class SelfTestTurnChecksTests
    {
        const string Vision = "capture_and_identify";

        [Test]
        public void FirstAudio_WithinBudget_Passes()
        {
            var r = new ScenarioResult("t");

            TurnChecks.FirstAudio(r, 1999);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Pass));
            Assert.That(r.Notes, Is.Empty);
        }

        [Test]
        public void FirstAudio_OverTheBudget_DegradesWithTheReason()
        {
            var r = new ScenarioResult("t");

            TurnChecks.FirstAudio(r, 2126);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Degraded));
            Assert.That(r.Notes, Is.EqualTo(new[] { "first audio over the 2.0 s budget" }));
        }

        [TestCase(4001)]
        [TestCase(double.NaN)]
        public void FirstAudio_LateOrMissing_Fails(double ms)
        {
            var r = new ScenarioResult("t");

            TurnChecks.FirstAudio(r, ms);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Fail));
            Assert.That(r.Notes, Does.Contain("no reply audio within 4 s of release"));
        }

        [Test]
        public void Tools_KeyToolFailed_Fails()
        {
            var r = new ScenarioResult("t");

            TurnChecks.Tools(r, new[] { Vision }, new[] { Vision + TurnObserver.FailedSuffix }, Vision);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Fail));
            Assert.That(r.Notes, Is.EqualTo(new[] { "capture_and_identify failed" }));
        }

        [Test]
        public void Tools_KeyToolSucceeded_Passes()
        {
            var r = new ScenarioResult("t");

            TurnChecks.Tools(r, new[] { Vision }, new[] { Vision }, Vision);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Pass));
        }

        [Test]
        public void Tools_KeyToolFailedThenSucceeded_Degrades()
        {
            var r = new ScenarioResult("t");

            TurnChecks.Tools(r, new[] { Vision, Vision }, new[] { Vision + TurnObserver.FailedSuffix, Vision }, Vision);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Degraded));
            Assert.That(r.Notes, Is.EqualTo(new[] { "capture_and_identify failed" }));
        }

        [Test]
        public void Tools_KeyToolNotCalled_Fails()
        {
            var r = new ScenarioResult("t");

            TurnChecks.Tools(r, new List<string>(), new List<string>(), Vision);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Fail));
            Assert.That(r.Notes, Is.EqualTo(new[] { "the model did not call capture_and_identify" }));
        }

        [Test]
        public void Tools_KeyToolStillRunning_Fails()
        {
            var r = new ScenarioResult("t");

            TurnChecks.Tools(r, new[] { Vision }, new List<string>(), Vision);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Fail));
            Assert.That(r.Notes, Is.EqualTo(new[] { "capture_and_identify did not complete" }));
        }

        [Test]
        public void Tools_UnexpectedToolFailedOnATurnWithoutAKeyTool_Degrades()
        {
            var r = new ScenarioResult("t");

            TurnChecks.Tools(r, new[] { "come_here" }, new[] { "come_here" + TurnObserver.FailedSuffix }, null);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Degraded));
            Assert.That(r.Notes, Is.EqualTo(new[] { "come_here failed" }));
        }

        [Test]
        public void PreferredTool_OtherToolChosen_DegradesWithItsName()
        {
            var r = new ScenarioResult("t");

            var called = TurnChecks.PreferredTool(r, new[] { Vision }, new[] { Vision }, "find_object");

            Assert.That(called, Is.False);
            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Degraded));
            Assert.That(r.Notes, Is.EqualTo(new[] { "the model called capture_and_identify instead of find_object" }));
        }

        [Test]
        public void PreferredTool_NoToolCalled_Degrades()
        {
            var r = new ScenarioResult("t");

            var called = TurnChecks.PreferredTool(r, new List<string>(), new List<string>(), "find_object");

            Assert.That(called, Is.False);
            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Degraded));
            Assert.That(r.Notes, Is.EqualTo(new[] { "the model called no tool instead of find_object" }));
        }

        [Test]
        public void PreferredTool_CalledButFailed_Fails()
        {
            var r = new ScenarioResult("t");

            var called = TurnChecks.PreferredTool(r, new[] { "find_object" },
                new[] { "find_object" + TurnObserver.FailedSuffix }, "find_object");

            Assert.That(called, Is.True);
            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Fail));
            Assert.That(r.Notes, Is.EqualTo(new[] { "find_object failed" }));
        }

        [Test]
        public void PreferredTool_OtherToolFailed_DegradesForBoth()
        {
            var r = new ScenarioResult("t");

            TurnChecks.PreferredTool(r, new[] { Vision }, new[] { Vision + TurnObserver.FailedSuffix }, "find_object");

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Degraded));
            Assert.That(r.Notes, Is.EqualTo(new[]
            {
                "capture_and_identify failed", "the model called capture_and_identify instead of find_object",
            }));
        }

        [Test]
        public void StayedPut_GreetingThatWalked_DegradesWithTheTool()
        {
            var r = new ScenarioResult("t");

            TurnChecks.StayedPut(r, new[] { "play_gesture", "come_here" });

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Degraded));
            Assert.That(r.Notes, Is.EqualTo(new[] { "moved without being asked (come_here)" }));
        }

        [Test]
        public void StayedPut_GestureOnly_Passes()
        {
            var r = new ScenarioResult("t");

            TurnChecks.StayedPut(r, new[] { "play_gesture", "look_at" });

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Pass));
        }

        [Test]
        public void ToolToAnswer_OverTheBudget_Degrades()
        {
            var r = new ScenarioResult("t");

            TurnChecks.ToolToAnswer(r, 8332);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Degraded));
            Assert.That(r.Notes, Is.EqualTo(new[] { "tool start → answer audio over the 2.5 s budget" }));
        }

        [Test]
        public void ToolToAnswer_NoAnswer_Fails()
        {
            var r = new ScenarioResult("t");

            TurnChecks.ToolToAnswer(r, double.NaN);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Fail));
        }

        [Test]
        public void TheReleaseRunVisionTurn_NowFailsInsteadOfPassing()
        {
            // dd14847 run 3: tools=capture_and_identify:failed, first audio 1,969 ms, tool → answer audio 8,332 ms.
            var r = new ScenarioResult("vision_capture_and_identify");

            TurnChecks.FirstAudio(r, 1968.7);
            TurnChecks.Tools(r, new[] { Vision }, new[] { Vision + TurnObserver.FailedSuffix }, Vision);
            TurnChecks.ToolToAnswer(r, 8332.1);

            Assert.That(r.Status, Is.EqualTo(ScenarioStatus.Fail));
            Assert.That(r.Notes, Is.EqualTo(new[]
            {
                "capture_and_identify failed", "tool start → answer audio over the 2.5 s budget",
            }));
        }

        [TestCase(new[] { "come_here" }, new[] { "guard_come_here" }, "guard")]
        [TestCase(new[] { "come_here", "come_here" }, new[] { "guard_come_here", "call_1" }, "guard+model")]
        [TestCase(new[] { "play_gesture", "come_here" }, new[] { "call_1", "call_2" }, "model")]
        [TestCase(new[] { "play_gesture" }, new[] { "call_1" }, "none")]
        [TestCase(new string[0], new string[0], "none")]
        public void ComeHereSource_SaysWhetherTheGuardOrTheModelStartedTheWalk(string[] started, string[] callIds,
                                                                              string expected)
        {
            Assert.That(TurnChecks.ComeHereSource(started, callIds), Is.EqualTo(expected));
        }
    }
}
