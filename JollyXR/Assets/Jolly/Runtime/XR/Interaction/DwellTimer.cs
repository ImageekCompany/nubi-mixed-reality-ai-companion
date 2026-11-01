namespace Jolly.XR.Interaction
{
    /// <summary>Fires once when a condition has held continuously for the dwell time; re-arms when it stops holding.</summary>
    public sealed class DwellTimer
    {
        readonly double _dwellSeconds;
        double _since = double.NaN;
        bool _fired;

        public DwellTimer(double dwellSeconds) => _dwellSeconds = dwellSeconds;

        public bool Update(bool holding, double now)
        {
            if (!holding)
            {
                _since = double.NaN;
                _fired = false;
                return false;
            }
            if (double.IsNaN(_since))
                _since = now;
            if (_fired || now - _since < _dwellSeconds)
                return false;
            _fired = true;
            return true;
        }
    }
}
