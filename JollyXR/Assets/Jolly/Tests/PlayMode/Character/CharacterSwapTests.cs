#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Jolly.App;
using Jolly.App.Platform;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Editor.Character;
using Jolly.Testing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>
    /// Swapping the character inside the Nubi shell. The second character is a copy of Nubi's definition under another
    /// id, so these run without any local character present.
    /// </summary>
    public sealed class CharacterSwapTests
    {
        NubiCharacterController _shell;
        CharacterDefinition _nubi;
        CharacterDefinition _other;
        string _dataRoot;

        [SetUp]
        public void SetUp()
        {
            _shell = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NubiAssetPaths.Prefab))
                           .GetComponent<NubiCharacterController>();
            _nubi = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(NubiAssetPaths.Definition);
            _other = Object.Instantiate(_nubi);
            _other.id = "other";
            _dataRoot = Path.Combine(Path.GetTempPath(), "jolly-character-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dataRoot);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_shell.gameObject);
            Object.Destroy(_other);
            Directory.Delete(_dataRoot, true);
        }

        GameObject[] ActiveModels() =>
            _shell.transform.Cast<Transform>().Where(t => t.name == "Model" && t.gameObject.activeSelf)
                  .Select(t => t.gameObject).ToArray();

        void Construct() => _shell.Construct(new StubUserPose(), new ScriptedLipSync(), new FakeClock(), new FakeRandom());

        [UnityTest]
        public IEnumerator TryUse_AnotherDefinition_ReplacesTheModelAndBindsIt()
        {
            // Arrange
            var before = ActiveModels().Single();

            // Act
            var used = _shell.TryUse(_other, out var error);
            Construct();
            yield return null;

            // Assert
            Assert.That(used, Is.True, error);
            Assert.That(_shell.Definition, Is.EqualTo(_other));
            Assert.That(before == null, Is.True, "the old model is destroyed");
            var model = ActiveModels().Single();
            Assert.That(_shell.Animator.transform.IsChildOf(model.transform), Is.True);
            Assert.That(_shell.FaceMesh.transform.IsChildOf(model.transform), Is.True);
            Assert.That(_shell.Animator.runtimeAnimatorController, Is.EqualTo(_other.animatorController));
        }

        [Test]
        public void TryUse_MissingBone_KeepsTheCurrentCharacter()
        {
            // Arrange
            _other.bones.head = "NoSuchHead";

            // Act
            var used = _shell.TryUse(_other, out var error);

            // Assert
            Assert.That(used, Is.False);
            Assert.That(error, Does.Contain("NoSuchHead"));
            Assert.That(_shell.Definition, Is.EqualTo(_nubi));
            Assert.That(ActiveModels(), Has.Length.EqualTo(1));
        }

        [Test]
        public void TryUse_NoModel_IsRefusedBeforeInstantiating()
        {
            _other.model = null;

            var used = _shell.TryUse(_other, out var error);

            Assert.That(used, Is.False);
            Assert.That(error, Is.EqualTo("no model"));
            Assert.That(_shell.transform.Cast<Transform>().Count(t => t.name == "Model"), Is.EqualTo(1));
        }

        [Test]
        public void TryUse_AfterConstruct_Throws()
        {
            Construct();

            Assert.Throws<InvalidOperationException>(() => _shell.TryUse(_other, out _));
        }

        [Test]
        public void Construct_SizesTheBodyFromTheDefinition()
        {
            // Arrange
            _other.height = 1.4f;
            _other.agentRadius = 0.3f;
            _other.colliderRadius = 0.35f;
            _other.scale = 1.5f;
            _other.contactShadowRadius = new Vector2(0.4f, 0.5f);

            // Act
            _shell.TryUse(_other, out _);
            Construct();

            // Assert
            var agent = _shell.GetComponent<NavMeshAgent>();
            var capsule = _shell.GetComponent<CapsuleCollider>();
            Assert.That(agent.height, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(agent.radius, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(capsule.height, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(capsule.radius, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(capsule.center.y, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(ActiveModels().Single().transform.localScale.x, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(_shell.transform.Find("BlobShadow").localScale, Is.EqualTo(new Vector3(0.8f, 1f, 1f)));
            Assert.That(_shell.Settings, Is.SameAs(_other.tuning));
        }

        [Test]
        public void Apply_OverrideFileNamesALocalCharacter_UsesIt()
        {
            // Arrange
            var paths = new AppPaths(_dataRoot);
            File.WriteAllText(paths.CharacterOverride, "Other\n");
            var config = Config();
            var log = new RecordingLog();

            // Act
            CharacterSelection.Apply(_shell, config, new CharacterChoice { localCharacters = new[] { _other } }, paths, log);

            // Assert
            Assert.That(_shell.Definition, Is.EqualTo(_other));
            Assert.That(log.Contains(LogLevel.Info, "evt=character.selected id=other"), Is.True);
            Object.Destroy(config);
        }

        [Test]
        public void Apply_BakedCharacterDoesNotBind_FallsBackToNubiWithAWarning()
        {
            // Arrange
            _other.bones.eyeLeft = "NoSuchEye";
            var config = Config();
            var log = new RecordingLog();
            var choice = new CharacterChoice { localCharacters = new[] { _other }, bakedId = "other" };

            // Act
            CharacterSelection.Apply(_shell, config, choice, new AppPaths(_dataRoot), log);

            // Assert
            Assert.That(_shell.Definition, Is.EqualTo(_nubi));
            Assert.That(log.Contains(LogLevel.Warning, "evt=character.bind_failed id=other"), Is.True);
            Assert.That(log.Contains(LogLevel.Info, "evt=character.selected id=nubi"), Is.True);
            Object.Destroy(config);
        }

        JollyConfig Config()
        {
            var config = ScriptableObject.CreateInstance<JollyConfig>();
            config.defaultCharacter = _nubi;
            config.characters = new[] { _nubi };
            return config;
        }
    }
}
#endif
