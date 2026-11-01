using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// The single writer of a character's face. Maps each <see cref="FaceChannel"/> through the definition's bindings
    /// onto blendshapes (a shape several channels drive takes the strongest) and onto the optional jaw bone. Resolves
    /// indices by name once; writes only changed blendshape weights. Names the mesh lacks are skipped (the editor
    /// validator reports them).
    /// </summary>
    public sealed class FaceRenderer
    {
        readonly SkinnedMeshRenderer _renderer;
        readonly int[] _shapes;
        readonly float[] _target;
        readonly float[] _written;
        readonly int[] _channel;
        readonly int[] _slot;
        readonly float[] _scale;
        readonly float[] _jawShare = new float[FaceChannels.Count];
        readonly Transform _jaw;
        readonly Quaternion _jawRest;
        readonly Vector3 _jawOpen;

        public FaceRenderer(SkinnedMeshRenderer renderer, IReadOnlyList<FaceChannelBinding> bindings,
                            Transform jaw = null, Vector3 jawOpenRotation = default)
        {
            _renderer = renderer;
            var mesh = renderer.sharedMesh;
            var shapes = new List<int>();
            var channels = new List<int>();
            var slots = new List<int>();
            var scales = new List<float>();
            foreach (var binding in bindings ?? Array.Empty<FaceChannelBinding>())
            {
                if (binding == null || !FaceChannels.TryParse(binding.channel, out var channel))
                    continue;
                _jawShare[(int)channel] = Mathf.Clamp01(binding.jawOpen);
                foreach (var target in binding.blendShapes ?? Array.Empty<BlendShapeTarget>())
                {
                    var index = target == null ? -1 : mesh.GetBlendShapeIndex(target.name);
                    if (index < 0)
                        continue;
                    var slot = shapes.IndexOf(index);
                    if (slot < 0)
                    {
                        slot = shapes.Count;
                        shapes.Add(index);
                    }
                    channels.Add((int)channel);
                    slots.Add(slot);
                    scales.Add(Mathf.Clamp01(target.weight));
                }
            }
            _shapes = shapes.ToArray();
            _target = new float[_shapes.Length];
            _written = new float[_shapes.Length];
            for (var s = 0; s < _written.Length; s++)
                _written[s] = -1f;
            _channel = channels.ToArray();
            _slot = slots.ToArray();
            _scale = scales.ToArray();

            _jaw = jaw;
            _jawRest = jaw != null ? jaw.localRotation : Quaternion.identity;
            _jawOpen = jawOpenRotation;
        }

        /// <param name="weights01">Indexed by <see cref="FaceChannel"/>, 0..1.</param>
        public void Write(float[] weights01)
        {
            Array.Clear(_target, 0, _target.Length);
            for (var m = 0; m < _channel.Length; m++)
            {
                var w = Mathf.Clamp01(weights01[_channel[m]]) * _scale[m];
                if (w > _target[_slot[m]])
                    _target[_slot[m]] = w;
            }
            for (var s = 0; s < _shapes.Length; s++)
            {
                var w = Mathf.Round(_target[s] * 1000f) * 0.1f;
                if (w == _written[s])
                    continue;
                _written[s] = w;
                _renderer.SetBlendShapeWeight(_shapes[s], w);
            }

            if (_jaw == null)
                return;
            var open = 0f;
            for (var c = 0; c < FaceChannels.Count; c++)
                open += Mathf.Clamp01(weights01[c]) * _jawShare[c];
            // Written every frame: an Animator that keys the jaw would otherwise leave a stale pose after a skipped write.
            _jaw.localRotation = _jawRest * Quaternion.Euler(_jawOpen * Mathf.Clamp01(open));
        }
    }
}
