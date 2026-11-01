using System.Collections.Generic;
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
        VoiceProfile _voice;
        CharacterDefinition _nubi;
        CharacterDefinition _jolly;
        CharacterDefinition _kiwi;
        CharacterDefinition[] _available;
        RecordingLog _log;

        [SetUp]
        public void SetUp()
        {
            _model = new GameObject("Model");
            _controller = new AnimatorOverrideController();
            _presets = new TextAsset("{\"presets\":[]}");
            _voice = ScriptableObject.CreateInstance<VoiceProfile>();
            _nubi = Definition("nubi");
            _jolly = Definition("jolly");
            _kiwi = Definition("kiwi");
            _available = new[] { _nubi, _jolly, _kiwi };
            _log = new RecordingLog();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_model);
            Object.Destroy(_controller);
            Object.Destroy(_presets);
            Object.Destroy(_voice);
            Object.Destroy(_nubi);
            Object.Destroy(_jolly);
            Object.Destroy(_kiwi);
        }

        CharacterDefinition Definition(string id)
        {
            var d = ScriptableObject.CreateInstance<CharacterDefinition>();
            d.id = id;
            d.model = _model;
            d.animatorController = _controller;
            d.facePresets = _presets;
            d.voice = _voice;
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

        [Test]
        public void Resolve_DefinitionWithoutVoice_IsUnusableSoItNeverSpeaksInAnotherCharactersVoice()
        {
            // Arrange
            _jolly.voice = null;

            // Act
            var chosen = CharacterSelection.Resolve(_available, _nubi, "jolly", null, _log);

            // Assert
            Assert.That(chosen, Is.EqualTo(_nubi));
            Assert.That(_log.Contains(LogLevel.Warning, "id=jolly reason=\"no voice\""), Is.True);
        }
    
        [Test]
        public void Bind_OverrideFailsToBind_TriesTheBakedIdBeforeTheFallback()
        {
            // Arrange
            var attempts = new List<string>();
            CharacterSelection.Binder bind = (CharacterDefinition d, out string error) =>
            {
                attempts.Add(d.id);
                error = d == _jolly ? "no face mesh" : null;
                return d != _jolly;
            };

            // Act
            var bound = CharacterSelection.Bind(_available, _nubi, "jolly", "kiwi", bind, _log);

            // Assert
            Assert.That(bound, Is.EqualTo(_kiwi));
            Assert.That(attempts, Is.EqualTo(new[] { "jolly", "kiwi" }));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=character.bind_failed id=jolly"), Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "evt=character.selected id=kiwi"), Is.True);
        }

        [Test]
        public void Bind_EveryRequestFailsToBind_BindsTheFallbackAndNeverRetriesAFailure()
        {
            // Arrange
            var attempts = new List<string>();
            CharacterSelection.Binder bind = (CharacterDefinition d, out string error) =>
            {
                attempts.Add(d.id);
                error = "broken";
                return d == _nubi;
            };

            // Act
            var bound = CharacterSelection.Bind(_available, _nubi, "jolly", "jolly", bind, _log);

            // Assert
            Assert.That(bound, Is.EqualTo(_nubi));
            Assert.That(attempts, Is.EqualTo(new[] { "jolly", "nubi" }));
        }

        [Test]
        public void Bind_NothingBinds_ReturnsNull()
        {
            CharacterSelection.Binder bind = (CharacterDefinition _, out string error) =>
            {
                error = "broken";
                return false;
            };

            var bound = CharacterSelection.Bind(_available, _nubi, null, "kiwi", bind, _log);

            Assert.That(bound, Is.Null);
        }
    }
}
