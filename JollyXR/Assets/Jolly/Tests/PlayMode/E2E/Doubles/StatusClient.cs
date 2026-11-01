using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Providers.MetaApi.Health;

namespace Jolly.Tests.PlayMode.E2E.Doubles
{
    /// <summary>GET /v1/models at the HTTP edge: 200 while online, 0 (connection error) while offline.</summary>
    public sealed class StatusClient : IHttpStatusClient
    {
        public bool Online { get; set; } = true;

        public UniTask<long> GetStatus(string url, string bearerToken, CancellationToken ct) =>
            UniTask.FromResult(Online ? 200L : 0L);
    }
}
