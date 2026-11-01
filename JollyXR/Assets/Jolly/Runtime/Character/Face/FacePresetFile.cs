using System;

namespace Jolly.Character
{
    /// <summary>JsonUtility shape of Nubi_FacePresets.json, written by tools/blender/nubi/faces.py.</summary>
    [Serializable]
    public sealed class FacePresetFile
    {
        public string[] loopingClips;
        public FacePresetEntry[] presets;
    }

    [Serializable]
    public sealed class FacePresetEntry
    {
        public string clip;
        public FaceWeightEntry[] weights;
    }

    [Serializable]
    public sealed class FaceWeightEntry
    {
        public string shape;
        public float weight;
    }
}
