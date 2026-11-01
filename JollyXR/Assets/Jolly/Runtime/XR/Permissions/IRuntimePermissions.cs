using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.XR.Permissions
{
    public interface IRuntimePermissions
    {
        bool IsGranted(string permission);
        /// <summary>Shows the system dialog(s). Completes on the main thread once every permission was answered or the
        /// dialog was dismissed; read the outcome with IsGranted.</summary>
        UniTask Request(IReadOnlyList<string> permissions, CancellationToken ct);
    }
}
