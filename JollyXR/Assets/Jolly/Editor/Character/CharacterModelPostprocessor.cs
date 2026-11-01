using System;
using System.IO;
using Jolly.Character;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Character
{
    /// <summary>
    /// Import settings for character models live in code, not in their .meta (docs/art/nubi/README.md, Unity notes;
    /// docs/characters.md). A model is a character model when a <c>&lt;Model&gt;_FacePresets.json</c> sits next to it,
    /// as the Blender builds write it. Generic rig, baked axis conversion (otherwise the rig object keeps a −90° X that
    /// Root compensates), bones kept as GameObjects for the look-at, and Loop Time on exactly the clips the build marks
    /// as loops.
    /// </summary>
    public sealed class CharacterModelPostprocessor : AssetPostprocessor
    {
        public const string FacePresetsSuffix = "_FacePresets.json";

        public override uint GetVersion() => 1;

        /// <summary>The face-preset file of a character model, or null when the asset is not one.</summary>
        public static string FacePresetsFor(string modelPath)
        {
            if (!modelPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return null;
            var presets = Path.Combine(Path.GetDirectoryName(modelPath) ?? "",
                                       Path.GetFileNameWithoutExtension(modelPath) + FacePresetsSuffix).Replace('\\', '/');
            return File.Exists(presets) ? presets : null;
        }

        void OnPreprocessModel()
        {
            if (FacePresetsFor(assetPath) == null)
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
            var presets = FacePresetsFor(assetPath);
            if (presets == null)
                return;
            context.DependsOnSourceAsset(presets);
            var looping = JsonUtility.FromJson<FacePresetFile>(File.ReadAllText(presets)).loopingClips
                          ?? Array.Empty<string>();

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
                clip.loopTime = Array.IndexOf(looping, clip.name) >= 0;
            importer.clipAnimations = clips;
        }
    }
}
