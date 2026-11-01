using System;
using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine.Events;

namespace Jolly.Integrations.ULipSync
{
    /// <summary>
    /// Adapts a <c>uLipSync</c> component (ADR-0007) to <see cref="ILipSyncDriver"/>: each vowel weight is its
    /// phoneme ratio times the normalised volume. uLipSync re-raises its last result every Update even when no new
    /// audio arrived (it only schedules analysis on received data), so a result whose raw volume has not changed for
    /// <see cref="SilenceSeconds"/> is treated as silence and the mouth closes.
    /// </summary>
    public sealed class ULipSyncDriver : ILipSyncDriver, IDisposable
    {
        public const double SilenceSeconds = 0.1;

        readonly uLipSync.uLipSync _source;
        readonly IClock _clock;
        readonly UnityAction<uLipSync.LipSyncInfo> _onUpdate;
        MouthShape _shape;
        float _lastRawVolume = -1f;
        double _lastChange = double.NegativeInfinity;
        bool _disposed;

        public ULipSyncDriver(uLipSync.uLipSync source, IClock clock)
        {
            _source = source;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            _onUpdate = OnLipSyncUpdate;
            source.onLipSyncUpdate.AddListener(_onUpdate);
        }

        public MouthShape Current => _clock.Now - _lastChange > SilenceSeconds ? MouthShape.Closed : _shape;

        public void Reset()
        {
            _shape = MouthShape.Closed;
            _lastChange = double.NegativeInfinity;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_source != null)
                _source.onLipSyncUpdate.RemoveListener(_onUpdate);
        }

        void OnLipSyncUpdate(uLipSync.LipSyncInfo info)
        {
            if (info.rawVolume == _lastRawVolume)
                return;
            _lastRawVolume = info.rawVolume;
            _lastChange = _clock.Now;

            var v = info.volume;
            var ratios = info.phonemeRatios;
            _shape = new MouthShape(Ratio(ratios, "A") * v, Ratio(ratios, "I") * v, Ratio(ratios, "U") * v,
                                    Ratio(ratios, "E") * v, Ratio(ratios, "O") * v, v);
        }

        static float Ratio(Dictionary<string, float> ratios, string phoneme) =>
            ratios != null && ratios.TryGetValue(phoneme, out var r) ? r : 0f;
    }
}
