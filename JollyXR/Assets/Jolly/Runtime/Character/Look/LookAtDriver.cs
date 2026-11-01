using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Turns Chest → Neck → Head, then the eyes, toward a world point on top of the animated pose (call after the
    /// Animator has evaluated). Rotations are about the body's own up/right axes, so they hold whatever the bones'
    /// local axes are. Blends in and out over <see cref="LookSettings.blendSeconds"/>.
    /// The look is relative, so it must start from the animated pose every frame: a bone still holding exactly what
    /// the last <see cref="Apply"/> wrote was not rewritten by the Animator (culled offscreen, or a bone no clip keys),
    /// and is put back to its pre-look rotation first instead of turning further each frame.
    /// </summary>
    public sealed class LookAtDriver
    {
        readonly NubiRigBones _bones;
        readonly Transform _body;
        readonly LookSettings _settings;
        readonly Transform[] _chain;
        readonly Quaternion[] _animated;
        readonly Quaternion[] _written;
        LookAngles _angles;
        float _weight;
        bool _bodyTurning;

        public LookAtDriver(NubiRigBones bones, Transform body, LookSettings settings)
        {
            _bones = bones;
            _body = body;
            _settings = settings;
            _chain = new[] { bones.chest, bones.neck, bones.head, bones.eyeLeft, bones.eyeRight };
            _animated = new Quaternion[_chain.Length];
            _written = new Quaternion[_chain.Length];
        }

        public float Weight => _weight;
        public LookAngles Angles => _angles;

        /// <summary>
        /// Applies the look and returns the yaw (degrees) the body should still turn, 0 when none. With
        /// <paramref name="face"/> the body turns past <see cref="LookSettings.faceUserBeyond"/>, else past
        /// <see cref="LookSettings.bodyTurnBeyond"/>.
        /// </summary>
        public float Apply(bool hasTarget, Vector3 targetWorld, bool suppressed, float deltaTime, bool face = false)
        {
            RestoreAnimatedPose();
            var goal = hasTarget && !suppressed ? 1f : 0f;
            _weight = Mathf.MoveTowards(_weight, goal, deltaTime / Mathf.Max(_settings.blendSeconds, 1e-3f));

            var bodyYaw = 0f;
            if (hasTarget)
            {
                LookAtSolver.Bearing(targetWorld - _bones.head.position, _body.forward, _body.up, out var yaw, out var pitch);
                _angles = LookAtSolver.Solve(yaw, pitch, _settings);
                bodyYaw = BodyTurn(yaw, face ? _settings.faceUserBeyond : _settings.bodyTurnBeyond);
            }
            else
            {
                _bodyTurning = false;
            }

            if (_weight > 0f)
                Pose(_angles, _weight);
            for (var i = 0; i < _chain.Length; i++)
                _written[i] = _chain[i].localRotation;
            return bodyYaw;
        }

        void RestoreAnimatedPose()
        {
            for (var i = 0; i < _chain.Length; i++)
            {
                var bone = _chain[i];
                if (bone.localRotation.Equals(_written[i]))
                    bone.localRotation = _animated[i];
                else
                    _animated[i] = bone.localRotation;
            }
        }

        float BodyTurn(float yaw, float beyond)
        {
            var off = Mathf.Abs(yaw);
            if (off > beyond)
                _bodyTurning = true;
            else if (off < _settings.bodySettleWithin)
                _bodyTurning = false;
            return _bodyTurning ? yaw : 0f;
        }

        void Pose(LookAngles a, float w)
        {
            var up = _body.up;
            var right = _body.right;
            var headShare = 1f - _settings.chestShare - _settings.neckShare;
            Rotate(_bones.chest, a.HeadYaw * _settings.chestShare * w, a.HeadPitch * _settings.chestShare * w, up, right);
            Rotate(_bones.neck, a.HeadYaw * _settings.neckShare * w, a.HeadPitch * _settings.neckShare * w, up, right);
            Rotate(_bones.head, a.HeadYaw * headShare * w, a.HeadPitch * headShare * w, up, right);
            Rotate(_bones.eyeLeft, a.EyeYaw * w, a.EyePitch * w, up, right);
            Rotate(_bones.eyeRight, a.EyeYaw * w, a.EyePitch * w, up, right);
        }

        static void Rotate(Transform bone, float yaw, float pitch, Vector3 up, Vector3 right)
        {
            bone.rotation = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right) * bone.rotation;
        }
    }
}
