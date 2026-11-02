using Jolly.App.SelfTest;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.App
{
    public sealed class SelfTestAssetsTests
    {
        SelfTestAssets _assets;

#if UNITY_EDITOR
        [Test]
        public void ShippedClips_TheGuardScenariosAreInTheTablesAndTheModelOneIsNot()
        {
            // Arrange
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<SelfTestAssets>("Assets/Jolly/Content/SelfTest/SelfTestAssets.asset");
            var guard = new Jolly.Core.Conversation.ActionGuard(new Jolly.Testing.FakeLocalizer());

            // Act + Assert
            Assert.That(guard.TryMatch(shipped.dance.expectedText, out _, out _), Is.True);
            Assert.That(guard.TryMatch(shipped.moveBehind.expectedText, out _, out _), Is.True);
            Assert.That(guard.TryMatch(shipped.danceThroughModel.expectedText, out _, out _), Is.False);
            Assert.That(shipped.danceThroughModel.clip, Is.Not.Null);
        }
#endif

        [SetUp]
        public void SetUp()
        {
            _assets = ScriptableObject.CreateInstance<SelfTestAssets>();
            _assets.comeHere = Utterance("Hi Nubi, come here.");
            _assets.comeHereByCharacter = new[]
            {
                new SelfTestAssets.CharacterUtterance { characterId = "jolly", utterance = Utterance("Hi Jolly, come here.") },
                new SelfTestAssets.CharacterUtterance { characterId = "kiwi", utterance = new SelfTestAssets.Utterance() },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_assets.comeHere.clip);
            Object.DestroyImmediate(_assets.comeHereByCharacter[0].utterance.clip);
            Object.DestroyImmediate(_assets);
        }

        [TestCase("jolly", "Hi Jolly, come here.")]
        [TestCase("nubi", "Hi Nubi, come here.")]
        [TestCase(null, "Hi Nubi, come here.")]
        [TestCase("kiwi", "Hi Nubi, come here.")]
        public void ComeHereFor_TheCharactersOwnLineWithAClip_ElseTheSharedOne(string id, string expected)
        {
            Assert.That(_assets.ComeHereFor(id).expectedText, Is.EqualTo(expected));
        }

        [Test]
        public void ShippedAssets_HaveJollysOwnComeHereLine()
        {
#if UNITY_EDITOR
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<SelfTestAssets>("Assets/Jolly/Content/SelfTest/SelfTestAssets.asset");
            var jolly = shipped.ComeHereFor("jolly");

            Assert.That(jolly.clip, Is.Not.Null);
            Assert.That(jolly.expectedText, Does.Contain("Jolly"));
#else
            Assert.Ignore("Loads the shipped asset through the AssetDatabase (Editor only).");
#endif
        }

        static SelfTestAssets.Utterance Utterance(string text) =>
            new() { clip = AudioClip.Create(text, 240, 1, 24000, false), expectedText = text };
    }
}
