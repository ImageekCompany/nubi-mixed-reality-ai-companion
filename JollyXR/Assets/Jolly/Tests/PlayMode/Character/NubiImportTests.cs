#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Jolly.Character;
using Jolly.Character.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class NubiImportTests
    {
        static ModelImporter Importer => (ModelImporter)AssetImporter.GetAtPath(NubiAssetPaths.Model);

        [Test]
        public void Model_IsGenericWithBakedAxisAndBoneObjects()
        {
            Assert.That(Importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
            Assert.That(Importer.bakeAxisConversion, Is.True);
            Assert.That(Importer.optimizeGameObjects, Is.False);
        }

        [Test]
        public void Clips_LoopExactlyTheClipsTheBlenderBuildMarksAsLoops()
        {
            var looping = JsonUtility.FromJson<FacePresetFile>(File.ReadAllText(NubiAssetPaths.FacePresets)).loopingClips;
            var clips = AssetDatabase.LoadAllAssetsAtPath(NubiAssetPaths.Model).OfType<AnimationClip>()
                                     .Where(c => !c.name.StartsWith("__preview__")).ToArray();

            Assert.That(clips.Select(c => c.name), Is.SupersetOf(new[] { "Idle", "Walk", "Hop", "Wave", "Shrug", "Stretch" }));
            foreach (var clip in clips)
                Assert.That(clip.isLooping, Is.EqualTo(looping.Contains(clip.name)), clip.name);
        }

        [Test]
        public void Model_HasEveryFaceBlendshape()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NubiAssetPaths.Model);
            var mesh = model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;

            foreach (var shape in FaceChannels.ShapeNames)
                Assert.That(mesh.GetBlendShapeIndex(shape), Is.GreaterThanOrEqualTo(0), shape);
        }

        [TestCase(NubiAssetPaths.SkinShader)]
        [TestCase(NubiAssetPaths.BlobShadowShader)]
        public void Shader_CompilesWithoutMessages(string name)
        {
            var shader = Shader.Find(name);

            Assert.That(shader, Is.Not.Null, name);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, name);
            Assert.That(ShaderUtil.GetShaderMessageCount(shader), Is.Zero, name);
            Assert.That(shader.isSupported, Is.True, name);
        }

        [Test]
        public void Model_UsesTheProjectMaterials()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NubiAssetPaths.Model);
            var materials = model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;

            Assert.That(materials.Select(AssetDatabase.GetAssetPath),
                        Is.EquivalentTo(new[] { NubiAssetPaths.SkinMaterial, NubiAssetPaths.GlossMaterial }));
            Assert.That(materials.Select(m => m.shader.name), Has.All.EqualTo(NubiAssetPaths.SkinShader));
        }
    }
}
#endif
