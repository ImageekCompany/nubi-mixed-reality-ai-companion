using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Presentation
{
    public enum Setting { CaptionSize, SpeechRate, Volume, Seated, PttHand, LatencyStats }

    /// <summary>Stepper semantics of the settings panel. Every step returns a new snapshot; the old one is never mutated.</summary>
    public static class SettingsModel
    {
        public const int MaxCaptionSize = 2;
        public const float RateStep = 0.25f, MinRate = 0.5f, MaxRate = 2f;
        public const float VolumeStep = 0.1f;

        public static UserPreferencesSnapshot Step(UserPreferencesSnapshot s, Setting setting, int direction)
        {
            var next = Copy(s);
            switch (setting)
            {
                case Setting.CaptionSize:
                    next.CaptionSize = Mathf.Clamp(s.CaptionSize + direction, 0, MaxCaptionSize);
                    break;
                case Setting.SpeechRate:
                    next.SpeechRate = Snap(s.SpeechRate + direction * RateStep, RateStep, MinRate, MaxRate);
                    break;
                case Setting.Volume:
                    next.Volume = Snap(s.Volume + direction * VolumeStep, VolumeStep, 0f, 1f);
                    break;
                case Setting.Seated:
                    next.Seated = !s.Seated;
                    break;
                case Setting.PttHand:
                    next.PttEitherHand = !s.PttEitherHand;
                    break;
                case Setting.LatencyStats:
                    next.ShowLatencyStats = !s.ShowLatencyStats;
                    break;
            }
            return next;
        }

        /// <summary>Brings stored values (possibly stale or corrupt) back onto the stepper's grid and ranges.</summary>
        public static UserPreferencesSnapshot Sanitize(UserPreferencesSnapshot s)
        {
            var defaults = new UserPreferencesSnapshot();
            var next = Copy(s);
            next.CaptionSize = Mathf.Clamp(s.CaptionSize, 0, MaxCaptionSize);
            next.SpeechRate = float.IsFinite(s.SpeechRate) ? Snap(s.SpeechRate, RateStep, MinRate, MaxRate) : defaults.SpeechRate;
            next.Volume = float.IsFinite(s.Volume) ? Snap(s.Volume, VolumeStep, 0f, 1f) : defaults.Volume;
            return next;
        }

        public static UserPreferencesSnapshot Copy(UserPreferencesSnapshot s) => new()
        {
            Seated = s.Seated,
            CaptionSize = s.CaptionSize,
            SpeechRate = s.SpeechRate,
            Volume = s.Volume,
            PttEitherHand = s.PttEitherHand,
            ShowLatencyStats = s.ShowLatencyStats,
            HomeCity = s.HomeCity,
            HomeCountry = s.HomeCountry,
        };

        /// <summary>Rounds to the step grid so repeated float steps never drift (0.1 * 3 != 0.3).</summary>
        static float Snap(float value, float step, float min, float max) =>
            Mathf.Clamp(Mathf.Round(value / step) * step, min, max);
    }
}
