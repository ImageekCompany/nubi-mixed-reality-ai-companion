namespace Jolly.Providers.MetaApi.Common
{
    /// <summary>
    /// What <see cref="MetaSseClient.Open"/> sent, for billing: an attempt the server refused with an HTTP status was
    /// not billed; any other (timed out, network error, cancelled, opened) may have been.
    /// </summary>
    public sealed class OpenAttempts
    {
        public int Started { get; private set; }
        public int RejectedWithStatus { get; private set; }
        public int PossiblyBilled => Started - RejectedWithStatus;

        internal void OnStarted() => Started++;

        internal void OnRejected() => RejectedWithStatus++;
    }
}
