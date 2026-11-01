using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Usage-meter reservation for one ASR request ($0.18 per audio hour). Settled once with the audio seconds actually
    /// sent, whatever the outcome: the server processed them either way.
    /// </summary>
    sealed class AsrBilling
    {
        readonly IApiUsageMeter _meter;
        readonly string _model;
        readonly long _reservation;
        bool _settled;

        AsrBilling(IApiUsageMeter meter, string model, long reservation)
        {
            _meter = meter;
            _model = model;
            _reservation = reservation;
        }

        public static AsrBilling Reserve(IApiUsageMeter meter, string model, decimal estimateUsd)
        {
            if (!meter.TryReserve(estimateUsd, out var reservation))
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.BudgetExceeded, "The API budget cap is reached.");
            return new AsrBilling(meter, model, reservation);
        }

        public void Settle(double audioSeconds)
        {
            if (_settled)
                return;
            _settled = true;
            _meter.Record(_reservation, new UsageRecord(_model, 0, 0, 0, audioSeconds: audioSeconds));
        }
    }
}
