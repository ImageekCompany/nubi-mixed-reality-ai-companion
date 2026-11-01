using UnityEngine;

namespace Jolly.Character
{
    /// <summary>Maps agent speed to the Locomotion state's playback rate so the planted feet match the ground speed.</summary>
    public static class LocomotionPlayback
    {
        const float StandingSpeed = 0.05f;

        public static float Rate(float speed, LocomotionSettings s)
        {
            if (speed < StandingSpeed)
                return 1f;
            var clipSpeed = speed > s.hopAbove ? s.hopClipSpeed : s.walkClipSpeed;
            return Mathf.Clamp(speed / clipSpeed, s.minPlaybackRate, s.maxPlaybackRate);
        }
    }
}
