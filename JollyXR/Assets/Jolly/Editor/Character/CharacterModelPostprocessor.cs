using System;
using System.IO;
using Jolly.Character;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Character
{
    /// <summary>
    /// Import settings for Nubi.fbx live in code, not in its .meta (docs/art/nubi/README.md, Unity notes): Generic
    /// rig, baked axis conversion (otherwise Nubi_Rig keeps a −90° X that Root compensates), bones kept as
    /// GameObjects for the look-at, and Loop Time on exactly the clips the Blender build marks as loops.
    /// </summary>
    public sealed class NubiModelPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (assetPath != NubiAssetPaths.Model)
                return;
            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importBlendShapes = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessAnimation()
        {
            if (assetPath != NubiAssetPaths.Model)
                return;
            context.DependsOnSourceAsset(NubiAssetPaths.FacePresets);
            var looping = JsonUtility.FromJson<FacePresetFile>(File.ReadAllText(NubiAssetPaths.FacePresets)).loopingClips
                          ?? Array.Empty<string>();

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
                clip.loopTime = Array.IndexOf(looping, clip.name) >= 0;
            importer.clipAnimations = clips;
        }
    }
}
