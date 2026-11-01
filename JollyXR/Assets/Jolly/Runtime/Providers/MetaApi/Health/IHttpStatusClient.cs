using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Providers.MetaApi.Health
{
    /// <summary>Bodiless authenticated GET. Returns the HTTP status, or 0 when no response arrived (DNS, TLS, socket).
    /// Throws only OperationCanceledException.</summary>
    public interface IHttpStatusClient
    {
        UniTask<long> GetStatus(string url, string bearerToken, CancellationToken ct);
    }
}
