using System.Linq;
using Jolly.Core.Tools;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.Eval
{
    public sealed class ToolSelectionGradingTests
    {
        [Test]
        public void Expected_CalledAlongsideAGesture_Passes()
        {
            var c = ToolSelectionCase.Calls("Hi Nubi, come here", ToolNames.ComeHere);

            Assert.That(c.Passes(new[] { ToolNames.Actions.Wave, ToolNames.ComeHere }), Is.True);
        }

        [Test]
        public void Expected_NotCalled_Fails()
        {
            var c = ToolSelectionCase.Calls("where is my mug?", ToolNames.FindObject);

            Assert.That(c.Passes(new[] { ToolNames.LookAt }), Is.False);
            Assert.That(c.Passes(new string[0]), Is.False);
        }

        [Test]
        public void AnyOfSeveralExpected_Passes()
        {
            var c = ToolSelectionCase.Calls("what is this?", ToolNames.CaptureAndIdentify, ToolNames.HighlightPart);

            Assert.That(c.Passes(new[] { ToolNames.HighlightPart }), Is.True);
        }

        [Test]
        public void StaysPut_AnswerInWordsOrAGesture_Passes()
        {
            var c = ToolSelectionCase.StaysPut("Hi Nubi");

            Assert.That(c.Passes(new string[0]), Is.True);
            Assert.That(c.Passes(new[] { ToolNames.Actions.Wave }), Is.True);
        }

        [TestCase(ToolNames.ComeHere)]
        [TestCase(ToolNames.WalkTo)]
        [TestCase(ToolNames.FollowUser)]
        [TestCase(ToolNames.GoToSurface)]
        public void StaysPut_AnyMovementTool_Fails(string movement)
        {
            var c = ToolSelectionCase.StaysPut("Hello, who are you?");

            Assert.That(c.Passes(new[] { ToolNames.Actions.Wave, movement }), Is.False);
        }

        [Test]
        public void Report_ListsEveryCaseWithItsToolsAndThePassRate()
        {
            var report = new ToolSelectionReport("abc1234", "minimal", "muse-spark-1.3");
            report.Add(ToolSelectionCase.Calls("come closer", ToolNames.ComeHere), new[] { ToolNames.ComeHere }, 812);
            report.Add(ToolSelectionCase.StaysPut("Hi Nubi"), new[] { ToolNames.ComeHere }, 640);
            report.Add(ToolSelectionCase.Calls("stop", ToolNames.Stop), new string[0], 500);

            var md = report.ToMarkdown(0.0012m);

            Assert.That(report.Passed, Is.EqualTo(1));
            Assert.That(md, Does.Contain("commit `abc1234`, effort `minimal`, model `muse-spark-1.3`"));
            Assert.That(md, Does.Contain("| come closer | `come_here` | `come_here` | 812 | PASS |"));
            Assert.That(md, Does.Contain("| Hi Nubi | no movement tool | `come_here` | 640 | **FAIL** |"));
            Assert.That(md, Does.Contain("| stop | `stop` | (none) | 500 | **FAIL** |"));
            Assert.That(md, Does.Contain("Pass rate: 1 / 3 (33 %)"));
            Assert.That(md, Does.Contain("$0.0012"));
        }

        [Test]
        public void Report_EscapesPipesInAnUtterance()
        {
            var report = new ToolSelectionReport("c", "low", "m");
            report.Add(ToolSelectionCase.StaysPut("a | b"), new string[0], 1);

            Assert.That(report.ToMarkdown(0m), Does.Contain("| a \\| b |"));
        }

        [Test]
        public void Select_ByTag_KeepsOnlyThoseCasesInOrder()
        {
            var selected = ToolSelectionCase.Select(new[] { ToolSelectionCase.Greeting, ToolNames.ComeHere });

            Assert.That(selected.Select(c => c.Utterance).Take(4),
                        Is.EqualTo(new[] { "Hi Nubi", "Hello, who are you?", "Good morning Nubi, how are you today?", "Nubi, come here" }));
            Assert.That(selected.Select(c => c.Tag).Distinct(), Is.EquivalentTo(new[] { ToolSelectionCase.Greeting, ToolNames.ComeHere }));
            Assert.That(ToolSelectionCase.Select(new string[0]), Is.SameAs(ToolSelectionCase.All));
        }

        [Test]
        public void Cases_CoverTheDeviceMissesAndAreUnique()
        {
            var utterances = ToolSelectionCase.All.Select(c => c.Utterance).ToArray();

            Assert.That(utterances, Is.Unique);
            Assert.That(utterances.Length, Is.InRange(18, 24));
            Assert.That(ToolSelectionCase.All.Count(c => c.Expected.Contains(ToolNames.ComeHere)), Is.GreaterThanOrEqualTo(4));
            Assert.That(ToolSelectionCase.All.Count(c => c.Expected.Contains(ToolNames.FindObject)), Is.GreaterThanOrEqualTo(3));
        }
    }
}
