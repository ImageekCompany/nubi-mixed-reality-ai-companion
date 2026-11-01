#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Jolly.App;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Editor.Character;
using NUnit.Framework;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>
    /// The rig contract (docs/characters.md) on every CharacterDefinition in the project. Here rather than in EditMode:
    /// EditMode tests reference no Character assembly (architecture §2).
    /// </summary>
    public sealed class CharacterDefinitionTests
    {
        /// <summary>The local demo skin docs/characters.md describes; git-ignored, so absent on CI and fresh clones.</summary>
        const string LocalJolly = LocalCharacters.Root + "/Characters/Jolly/JollyCharacter.asset";
        const string ConfigPath = "Assets/Jolly/Content/Config/JollyConfig.asset";

        static IEnumerable<string> Definitions()
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal) { NubiAssetPaths.Definition, LocalJolly };
            paths.UnionWith(CharacterDefinitionValidator.FindAll());
            return paths;
        }

        [TestCaseSource(nameof(Definitions))]
        public void Definition_MeetsTheRigContract(string path)
        {
            // Arrange
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (definition == null && LocalCharacters.IsLocal(path))
                Assert.Ignore($"{path} is a local, git-ignored character that is not present here (docs/characters.md).");
            Assert.That(definition, Is.Not.Null, path);

            // Act
            var errors = CharacterDefinitionValidator.Validate(definition)
                                                     .Where(i => i.Level == CharacterIssueLevel.Error)
                                                     .Select(i => i.Message);

            // Assert
            Assert.That(errors, Is.Empty, path);
        }

        [Test]
        public void Config_DefaultsToTheCommittedNubi()
        {
            var config = AssetDatabase.LoadAssetAtPath<JollyConfig>(ConfigPath);
            var nubi = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(NubiAssetPaths.Definition);

            Assert.That(config.defaultCharacter, Is.EqualTo(nubi));
            Assert.That(config.characters, Does.Contain(nubi));
            Assert.That(config.characters.Select(AssetDatabase.GetAssetPath), Has.None.StartsWith(LocalCharacters.Root));
        }

        [Test]
        public void NubiPrefab_ShipsNubisDefinition()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(NubiAssetPaths.Prefab);

            var controller = prefab.GetComponent<NubiCharacterController>();

            Assert.That(controller.Definition, Is.EqualTo(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(NubiAssetPaths.Definition)));
        }

        [Test]
        public void RequiredClips_CoverEveryGestureClip()
        {
            foreach (GestureId gesture in Enum.GetValues(typeof(GestureId)))
            {
                if (GestureClips.TryGetClip(gesture, out var clip))
                    Assert.That(CharacterClips.Required, Does.Contain(clip), gesture.ToString());
            }
        }

        [Test]
        public void Validate_BrokenCopyOfNubi_ReportsEachBrokenPart()
        {
            // Arrange
            var broken = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(NubiAssetPaths.Definition));
            broken.id = "Not An Id";
            broken.bones = new RigBoneNames { head = "NoSuchHead" };
            broken.face = broken.face.Where(b => b.channel != "Blink_R").ToArray();
            broken.face[0] = new FaceChannelBinding
            {
                channel = broken.face[0].channel,
                blendShapes = new[] { new BlendShapeTarget { name = "NoSuchShape", weight = 1f } },
            };
            broken.clips = new[] { new ClipBinding { name = "Dance" } };

            try
            {
                // Act
                var errors = CharacterDefinitionValidator.Validate(broken)
                                                         .Where(i => i.Level == CharacterIssueLevel.Error)
                                                         .Select(i => i.Message).ToArray();

                // Assert
                Assert.That(errors, Has.Some.Contains("Not An Id"));
                Assert.That(errors, Has.Some.Contains("NoSuchHead"));
                Assert.That(errors, Has.Some.Contains("NoSuchShape"));
                Assert.That(errors, Has.Some.Contains("'Blink_R' is not bound"));
                Assert.That(errors, Has.Some.Contains("'Dance'"));
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        [Test]
        public void Validate_NoVoice_IsAnError()
        {
            // Arrange
            var mute = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(NubiAssetPaths.Definition));
            mute.voice = null;

            try
            {
                // Act
                var errors = CharacterDefinitionValidator.Validate(mute).Where(i => i.Level == CharacterIssueLevel.Error)
                                                         .Select(i => i.Message);

                // Assert
                Assert.That(errors, Has.Some.Contains("voice is not set"));
            }
            finally
            {
                Object.DestroyImmediate(mute);
            }
        }

        [Test]
        public void Validate_VoiceWithoutAnEnglishWitVoice_IsAnError()
        {
            // Arrange
            var definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(NubiAssetPaths.Definition));
            var voice = UnityEngine.ScriptableObject.CreateInstance<VoiceProfile>();
            voice.defaultVoice.voiceId = " ";
            definition.voice = voice;

            try
            {
                // Act
                var errors = CharacterDefinitionValidator.Validate(definition).Where(i => i.Level == CharacterIssueLevel.Error)
                                                         .Select(i => i.Message);

                // Assert
                Assert.That(errors, Has.Some.Contains("no English Wit voice"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(voice);
            }
        }

        [Test]
        public void Validate_JawOnlyViseme_IsAccepted()
        {
            // Arrange: A shown by the jaw alone, with the head bone standing in for a jaw.
            var jawed = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(NubiAssetPaths.Definition));
            jawed.jaw = new JawBinding { bone = jawed.bones.head, openRotation = new UnityEngine.Vector3(15f, 0f, 0f) };
            var a = jawed.face.First(b => b.channel == "A");
            a.blendShapes = Array.Empty<BlendShapeTarget>();
            a.jawOpen = 1f;

            try
            {
                // Act
                var errors = CharacterDefinitionValidator.Validate(jawed).Where(i => i.Level == CharacterIssueLevel.Error);

                // Assert
                Assert.That(errors, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(jawed);
            }
        }
    }
}
#endif
