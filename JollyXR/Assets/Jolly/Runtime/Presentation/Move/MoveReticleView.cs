using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>
    /// <see cref="IMoveMarker"/>: a small ring lying on the floor under the user's ray, and a short outward pulse where a
    /// move was sent (ADR-0044). While it pulses, Show and Hide wait for it to finish, so the confirmation is seen even
    /// when the ray stays on the floor. Keep this object at the scene root with identity scale: the ring uses world poses.
    /// </summary>
    public sealed class MoveReticleView : MonoBehaviour, IMoveMarker
    {
        // The shader's default ring radius in uv units of the quad.
        const float RingUv = 0.36f;
        // Above the floor plane, which the scene occluders also draw into the depth buffer.
        const float Lift = 0.01f;
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [SerializeField] Material _material;
        [SerializeField] float _radiusMeters = 0.1f;
        [SerializeField] float _fadeInSeconds = 0.08f;
        [SerializeField] float _pulseSeconds = 0.45f;
        [SerializeField] float _pulseScale = 1.8f;

        // Created with the ring: Unity refuses a MaterialPropertyBlock in a MonoBehaviour field initializer.
        MaterialPropertyBlock _properties;
        IClock _clock;
        Mesh _mesh;
        GameObject _ring;
        MeshRenderer _renderer;
        double _shownAt;
        double _pulseAt = double.NegativeInfinity;
        bool _visible;

        public bool IsVisible => _ring != null && _ring.activeSelf;
        public bool IsPulsing => _clock != null && _clock.Now - _pulseAt < _pulseSeconds;
        public Vector3 Position => _ring != null ? _ring.transform.position - Vector3.up * Lift : default;

        public void Construct(IClock clock) => _clock = clock;

        public void Show(Vector3 floorPoint)
        {
            if (IsPulsing)
                return;
            EnsureRing();
            Place(floorPoint);
            if (_visible)
                return;
            _visible = true;
            _shownAt = _clock.Now;
            SetAlpha(0f);
        }

        public void Hide()
        {
            _visible = false;
            if (!IsPulsing && _ring != null)
                _ring.SetActive(false);
        }

        public void Confirm(Vector3 floorPoint)
        {
            EnsureRing();
            _visible = false;
            _pulseAt = _clock.Now;
            Place(floorPoint);
            SetAlpha(1f);
        }

        void Update()
        {
            if (_ring == null || !_ring.activeSelf || _clock == null)
                return;
            var pulse = (float)(_clock.Now - _pulseAt) / _pulseSeconds;
            if (pulse < 1f)
            {
                _ring.transform.localScale = Vector3.one * (Diameter * Mathf.Lerp(1f, _pulseScale, pulse));
                SetAlpha(1f - pulse);
                return;
            }
            if (!_visible)
            {
                _ring.SetActive(false);
                return;
            }
            _ring.transform.localScale = Vector3.one * Diameter;
            SetAlpha(_fadeInSeconds > 0f ? Mathf.Clamp01((float)(_clock.Now - _shownAt) / _fadeInSeconds) : 1f);
        }

        float Diameter => _radiusMeters / RingUv;

        void Place(Vector3 floorPoint)
        {
            _ring.transform.SetPositionAndRotation(floorPoint + Vector3.up * Lift, Quaternion.Euler(90f, 0f, 0f));
            _ring.transform.localScale = Vector3.one * Diameter;
            _ring.SetActive(true);
        }

        void SetAlpha(float alpha)
        {
            _properties.SetFloat(AlphaId, alpha);
            _renderer.SetPropertyBlock(_properties);
        }

        void EnsureRing()
        {
            if (_ring != null)
                return;
            _mesh = QuadMesh.Create();
            _properties = new MaterialPropertyBlock();
            _ring = new GameObject("MoveRing");
            _ring.transform.SetParent(transform, false);
            _ring.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _ring.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _ring.SetActive(false);
        }

        void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }
    }
}
