using Jolly.App;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.App
{
    public sealed class CharacterSelectionTests
    {
        GameObject _model;
        AnimatorOverrideController _controller;
        TextAsset _presets;
        CharacterDefinition _nubi;
        CharacterDefinition _jolly;
        CharacterDefinition[] _available;
        RecordingLog _log;

        [SetUp]
        public void SetUp()
        {
            _model = new GameObject("Model");
            _controller = new AnimatorOverrideController();
            _presets = new TextAsset("{\"presets\":[]}");
            _nubi = Definition("nubi");
            _jolly = Definition("jolly");
            _available = new[] { _nubi, _jolly };
            _log = new RecordingLog();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_model);
            Object.Destroy(_controller);
            Object.Destroy(_presets);
            Object.Destroy(_nubi);
            Object.Destroy(_jolly);
        }

        CharacterDefinition Definition(string id)
        {
            var d = ScriptableObject.CreateInstance<CharacterDefinition>();
            d.id = id;
            d.model = _model;
            d.animatorController = _controller;
            d.facePresets = _presets;
            return d;
        }

        [Test]
        public void Resolve_NoIds_ReturnsTheFallback()
        {
            var chosen = CharacterSelection.Resolve(_available, _nubi, null, "", _log);

            Assert.That(chosen, Is.EqualTo(_nubi));
            Assert.That(_log.Entries, Is.Empty);
        }

        [Test]
        public void Resolve_OverrideId_WinsOverTheBakedId()
        {
            var chosen = CharacterSelection.Resolve(_available, _nubi, " JOLLY ", "nubi", _log);

            Assert.That(chosen, Is.EqualTo(_jolly));
        }

        [Test]
        public void Resolve_BakedId_UsedWithoutAnOverride()
        {
            var chosen = CharacterSelection.Resolve(_available, _nubi, null, "jolly", _log);

            Assert.That(chosen, Is.EqualTo(_jolly));
        }

        [Test]
        public void Resolve_UnknownOverride_WarnsAndFallsThroughToTheBakedId()
        {
            var chosen = CharacterSelection.Resolve(_available, _nubi, "yeti", "jolly", _log);

            Assert.That(chosen, Is.EqualTo(_jolly));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=character.unknown source=override id=yeti"), Is.True);
        }

        [Test]
        public void Resolve_UnusableDefinition_WarnsAndFallsBack()
        {
            // Arrange
            _jolly.facePresets = null;

            // Act
            var chosen = CharacterSelection.Resolve(_available, _nubi, "jolly", null, _log);

            // Assert
            Assert.That(chosen, Is.EqualTo(_nubi));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=character.unusable source=override id=jolly"), Is.True);
        }
    }
}
