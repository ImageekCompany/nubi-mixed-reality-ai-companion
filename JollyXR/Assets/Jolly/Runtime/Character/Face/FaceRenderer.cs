using UnityEngine;

namespace Jolly.Character
{
    /// <summary>The single writer of Nubi's blendshapes. Resolves indices by name once; writes only changed weights.</summary>
    public sealed class FaceRenderer
    {
        readonly SkinnedMeshRenderer _renderer;
        readonly int[] _index = new int[FaceChannels.Count];
        readonly float[] _written = new float[FaceChannels.Count];

        public FaceRenderer(SkinnedMeshRenderer renderer)
        {
            _renderer = renderer;
            var mesh = renderer.sharedMesh;
            for (var c = 0; c < FaceChannels.Count; c++)
            {
                _index[c] = mesh.GetBlendShapeIndex(FaceChannels.ShapeNames[c]);
                _written[c] = -1f;
            }
        }

        public bool Has(FaceChannel channel) => _index[(int)channel] >= 0;

        /// <summary>Unity blendshape weight (0..100) currently applied.</summary>
        public float Weight(FaceChannel channel)
        {
            var i = _index[(int)channel];
            return i >= 0 ? _renderer.GetBlendShapeWeight(i) : 0f;
        }

        public void Write(float[] weights01)
        {
            for (var c = 0; c < FaceChannels.Count; c++)
            {
                var i = _index[c];
                if (i < 0)
                    continue;
                var w = Mathf.Round(Mathf.Clamp01(weights01[c]) * 1000f) * 0.1f;
                if (w == _written[c])
                    continue;
                _written[c] = w;
                _renderer.SetBlendShapeWeight(i, w);
            }
        }
    }
}
