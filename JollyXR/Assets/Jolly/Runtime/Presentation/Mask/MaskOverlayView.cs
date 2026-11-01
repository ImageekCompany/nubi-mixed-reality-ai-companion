using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>
    /// IMaskPresenter: one outlined quad per segmented object, placed in world space from the capture camera
    /// (<see cref="MaskPlacement"/>). Fades out after the visible time, on <see cref="Clear"/>, or when the next turn
    /// starts listening. Keep this object at the scene root with identity scale: quads use world poses.
    /// </summary>
    public sealed class MaskOverlayView : MonoBehaviour, IMaskPresenter
    {
        [SerializeField] Material _material;
        [Tooltip("SAM returns at most 16 instances for a text prompt.")]
        [SerializeField] int _capacity = 16;
        [SerializeField] float _visibleSeconds = 6f;
        [SerializeField] float _fadeSeconds = 0.5f;

        readonly List<MaskQuad> _quads = new();
        IClock _clock;
        IConversationEvents _events;
        Mesh _mesh;
        double _shownAt = double.NegativeInfinity;
        int _visibleCount;

        public int VisibleCount => _visibleCount;

        public void Construct(IClock clock, IConversationEvents events)
        {
            Unsubscribe();
            _clock = clock;
            _events = events;
            _events.StateChanged += OnStateChanged;
        }

        /// <summary>The quad showing objects[index] of the last <see cref="Show"/>.</summary>
        public Transform QuadAt(int index) => _quads[index].Transform;
        public Texture2D TextureAt(int index) => _quads[index].Texture;

        public void Show(IReadOnlyList<SegmentedObject> objects, EncodedImage source, float distanceMeters)
        {
            Clear();
            var count = Mathf.Min(objects.Count, _capacity);
            for (var i = 0; i < count; i++)
            {
                var obj = objects[i];
                var pose = MaskPlacement.Place(obj.Box, source.Intrinsics, source.CameraPoseWorld, distanceMeters, out var size);
                Quad(i).Show(pose, size, obj.Mask);
            }
            _visibleCount = count;
            _shownAt = _clock.Now;
        }

        public void Clear()
        {
            for (var i = 0; i < _visibleCount; i++)
                _quads[i].Hide();
            _visibleCount = 0;
        }

        void Update()
        {
            if (_visibleCount == 0)
                return;
            var age = (float)(_clock.Now - _shownAt);
            if (age < _visibleSeconds)
                return;
            if (age >= _visibleSeconds + _fadeSeconds)
            {
                Clear();
                return;
            }
            var fade = 1f - (age - _visibleSeconds) / _fadeSeconds;
            for (var i = 0; i < _visibleCount; i++)
                _quads[i].SetFade(fade);
        }

        MaskQuad Quad(int index)
        {
            if (_mesh == null)
                _mesh = QuadMesh.Create();
            while (_quads.Count <= index)
                _quads.Add(new MaskQuad(transform, _mesh, _material, _quads.Count));
            return _quads[index];
        }

        void OnStateChanged(ConversationState from, ConversationState to)
        {
            if (to == ConversationState.Listening)
                Clear();
        }

        void OnDestroy()
        {
            Unsubscribe();
            foreach (var quad in _quads)
                quad.Destroy();
            _quads.Clear();
            if (_mesh != null)
                Destroy(_mesh);
        }

        void Unsubscribe()
        {
            if (_events != null)
                _events.StateChanged -= OnStateChanged;
            _events = null;
        }
    }
}
