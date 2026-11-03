using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Memory;
using Jolly.Core.Tools;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    /// <summary>Which phrases skip the model (ADR-0051), the first two lines from the 2026-11-03 live misses.</summary>
    public sealed class PlaceGuardTests
    {
        SceneMemory _scene;
        PlaceGuard _guard;

        [SetUp]
        public void SetUp()
        {
            _scene = new SceneMemory("unused", new FakeSpatialAnchors(), new FakeClock(), new MemoryLimits(), new RecordingLog());
            _guard = new PlaceGuard(new FakeLocalizer(), _scene);
        }

        void Remember(string name) =>
            _scene.RememberPlace(new PlaceRecord { Name = name, Pose = new Pose(Vector3.one, Quaternion.identity) });

        [TestCase("That over there is the kitchen.", "kitchen")]
        [TestCase("This is my kitchen.", "kitchen")]
        [TestCase("Jolly, this is the kitchen", "kitchen")]
        [TestCase("That's the living room.", "living room")]
        [TestCase("The kitchen is over there.", "kitchen")]
        [TestCase("Here's the bathroom", "bathroom")]
        public void ShowingARoom_RemembersWhereTheUserIsLooking(string transcript, string room)
        {
            Assert.That(_guard.TryMatch(transcript, out var tool, out var json, out var named), Is.True);

            Assert.That(tool, Is.EqualTo(ToolNames.RememberPlace));
            Assert.That(named, Is.EqualTo(room));
            Assert.That(json, Is.EqualTo("{\"name\":\"" + room + "\",\"where\":\"there\"}"));
        }

        [TestCase("Is this the kitchen?")]
        [TestCase("This is not the kitchen.")]
        [TestCase("This is the best.")]
        [TestCase("That is a nice mug.")]
        [TestCase("I was in the kitchen and I saw a really big spider today, it was scary.")]
        [TestCase("Go to the kitchen.")]
        [TestCase("")]
        public void OtherPhrases_StayWithTheModel(string transcript)
        {
            Remember("garage");

            Assert.That(_guard.TryMatch(transcript, out _, out _, out _), Is.False);
        }

        [TestCase("Where is the kitchen?")]
        [TestCase("Where's my kitchen?")]
        [TestCase("Show me the kitchen.")]
        [TestCase("Jolly, take me to the kitchen")]
        public void AskingAfterARememberedPlace_LeadsThere(string transcript)
        {
            Remember("kitchen");

            Assert.That(_guard.TryMatch(transcript, out var tool, out var json, out _), Is.True);

            Assert.That(tool, Is.EqualTo(ToolNames.ShowPlace));
            Assert.That(json, Is.EqualTo("{\"place\":\"kitchen\"}"));
        }

        [Test]
        public void AskingAfterAPlaceNeverShown_StaysWithTheModel()
        {
            Remember("garage");

            Assert.That(_guard.TryMatch("Where is the kitchen?", out _, out _, out _), Is.False);
        }

        [TestCase("Where can I put these things?", true)]
        [TestCase("Where do these go?", true)]
        [TestCase("Where does this go?", true)]
        [TestCase("What is this?", false)]
        public void WhereItGoes_NeedsARememberedPlace(string transcript, bool expected)
        {
            Assert.That(_guard.AsksWhereItGoes(transcript), Is.False, "no place to name yet");

            Remember("kitchen");

            Assert.That(_guard.AsksWhereItGoes(transcript), Is.EqualTo(expected));
        }
    }
}
