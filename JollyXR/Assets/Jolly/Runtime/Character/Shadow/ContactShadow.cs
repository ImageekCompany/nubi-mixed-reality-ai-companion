using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Grounds Nubi on the real floor: the blob quad is a child of the root, which the NavMesh agent (or NubiPlacement)
    /// keeps on the floor, so it already follows Nubi at floor height; this only shrinks and fades it as the feet leave
    /// the floor. Opacity goes through a property block, so the one shadow draw leaves the SRP Batcher; it is a single
    /// quad either way.
    /// </summary>
    sealed class ContactShadow
    {
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        const float Epsilon = 1e-3f;

        readonly ContactShadowRig _rig;
        readonly Transform _root;
        readonly ContactShadowSettings _settings;
        readonly Transform _quad;
        readonly Vector3 _standingScale;
        readonly float _standingOpacity;
        readonly MaterialPropertyBlock _block = new();
        ContactShadowShape _applied = new(1f, 1f);

        public ContactShadow(ContactShadowRig rig, Transform root, ContactShadowSettings settings)
        {
            _rig = rig;
            _root = root;
            _settings = settings;
            _quad = rig.shadow.transform;
            _standingScale = _quad.localScale;
            _standingOpacity = rig.shadow.sharedMaterial.GetFloat(OpacityId);
        }

        public void Update()
        {
            var lift = ContactShadowMath.Lift(_rig.footLeft.position.y, _rig.footRight.position.y, _root.position.y,
                _rig.footRestHeight);
            var shape = ContactShadowMath.Shape(lift, _settings);
            if (Mathf.Abs(shape.Opacity - _applied.Opacity) < Epsilon && Mathf.Abs(shape.Scale - _applied.Scale) < Epsilon)
                return;
            _applied = shape;
            _quad.localScale = new Vector3(_standingScale.x * shape.Scale, _standingScale.y * shape.Scale, _standingScale.z);
            _block.SetFloat(OpacityId, _standingOpacity * shape.Opacity);
            _rig.shadow.SetPropertyBlock(_block);
        }
    }
}
