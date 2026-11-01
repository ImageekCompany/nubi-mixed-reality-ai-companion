using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// The scene's mask overlay with a record of the last masks shown: when, how many and for which capture, so a vision
    /// scenario can tell that its tool outlined something on the frame it asked about.
    /// </summary>
    public sealed class MaskObserver : IMaskPresenter
    {
        readonly IMaskPresenter _inner;
        readonly IClock _clock;

        public MaskObserver(IMaskPresenter inner, IClock clock)
        {
            _inner = inner;
            _clock = clock;
        }

        /// <summary><see cref="IClock.Now"/> of the last <see cref="Show"/>; NaN before the first.</summary>
        public double ShownAt { get; private set; } = double.NaN;
        public int ShownCount { get; private set; }
        public EncodedImage ShownSource { get; private set; }

        public void Show(IReadOnlyList<SegmentedObject> objects, EncodedImage source, float distanceMeters)
        {
            _inner.Show(objects, source, distanceMeters);
            ShownAt = _clock.Now;
            ShownCount = objects.Count;
            ShownSource = source;
        }

        public void Clear() => _inner.Clear();
    }
}
