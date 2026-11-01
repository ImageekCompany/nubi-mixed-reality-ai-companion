using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Per-clip face weights (0..1), keyed by Animator state hash: every Nubi state is named after its clip.
    /// The FBX takes carry no blendshape curves, so this table is the only source of a clip's expression.
    /// </summary>
    public sealed class FacePresetTable
    {
        readonly Dictionary<int, float[]> _byState = new();

        FacePresetTable() { }

        public int Count => _byState.Count;

        public static FacePresetTable Parse(string json)
        {
            var file = JsonUtility.FromJson<FacePresetFile>(json);
            if (file?.presets == null)
                throw new FormatException("Face preset JSON has no 'presets' array.");

            var table = new FacePresetTable();
            foreach (var preset in file.presets)
            {
                var weights = new float[FaceChannels.Count];
                foreach (var w in preset.weights ?? Array.Empty<FaceWeightEntry>())
                {
                    if (!FaceChannels.TryParse(w.shape, out var channel))
                        throw new FormatException($"Face preset '{preset.clip}' uses unknown shape '{w.shape}'.");
                    weights[(int)channel] = Mathf.Clamp01(w.weight);
                }
                table._byState[Animator.StringToHash(preset.clip)] = weights;
            }
            return table;
        }

        /// <summary>Weights indexed by <see cref="FaceChannel"/>; null when the state has no preset. Do not mutate.</summary>
        public float[] ForState(int stateNameHash) => _byState.TryGetValue(stateNameHash, out var w) ? w : null;

        public float[] ForClip(string clip) => ForState(Animator.StringToHash(clip));
    }
}
