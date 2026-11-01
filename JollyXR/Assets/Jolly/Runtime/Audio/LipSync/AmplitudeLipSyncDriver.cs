using System;
using Jolly.Audio.Output;
using Jolly.Contracts;

namespace Jolly.Audio.LipSync
{
    /// <summary>
    /// Fallback lipsync (ADR-0007): the speech output's block RMS opens the mouth on "A". Attack is instant; release
    /// decays with a 20 ms time constant, so the mouth reads Closed about 100 ms into silence. <see cref="Current"/>
    /// advances the envelope by the clock time since the previous read, so poll it once per frame (LateUpdate).
    /// </summary>
    public sealed class AmplitudeLipSyncDriver : ILipSyncDriver
    {
        const float Gain = 4f;
        const double ReleaseSeconds = 0.020;
        const float ClosedBelow = 0.01f;

        readonly ISpeechLevel _level;
        readonly IClock _clock;
        float _envelope;
        double _lastRead;

        public AmplitudeLipSyncDriver(ISpeechLevel level, IClock clock)
        {
            _level = level;
            _clock = clock;
            _lastRead = clock.Now;
        }

        public MouthShape Current
        {
            get
            {
                var now = _clock.Now;
                var elapsed = now - _lastRead;
                _lastRead = now;

                var level = _level.Level;
                _envelope = level >= _envelope ? level : Math.Max(level, _envelope * (float)Math.Exp(-elapsed / ReleaseSeconds));
                if (_envelope < ClosedBelow)
                {
                    _envelope = 0f;
                    return MouthShape.Closed;
                }
                return new MouthShape(Math.Min(1f, _envelope * Gain), 0f, 0f, 0f, 0f, _envelope);
            }
        }

        public void Reset() => _envelope = 0f;
    }
}
