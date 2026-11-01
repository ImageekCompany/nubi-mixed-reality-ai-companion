using System;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Common
{
    /// <summary>
    /// ToS §6.2 guard used by every MetaApi adapter: live user data never goes to a -contributor model. Ordinary code,
    /// not Debug.Assert, so it survives release builds (ADR-0004).
    /// </summary>
    public static class MetaModelResolver
    {
        const string ContributorSuffix = "-contributor";

        public static string Resolve(ModelRoute route, string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ProviderException(MetaErrors.Provider, ProviderErrorKind.BadRequest, $"No model id configured for route {route}.");
            if (route == ModelRoute.LiveUser && modelId.EndsWith(ContributorSuffix, StringComparison.OrdinalIgnoreCase))
                throw new ProviderException(MetaErrors.Provider, ProviderErrorKind.BadRequest,
                    $"Model '{modelId}' is a contributor model and must not receive live user data (route {route}).");
            return modelId;
        }
    }
}
