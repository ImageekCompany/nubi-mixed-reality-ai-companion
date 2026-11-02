using Jolly.Contracts;
using Jolly.Core.Secrets;

namespace Jolly.App.Platform
{
    /// <summary>
    /// The secrets source per platform (ADR-0010): the pushed persistentDataPath/secrets.json on device, the repository
    /// .env in the Editor. Nothing secret is serialized into a build unless build.sh --embed-secrets asks for it (ADR-0049).
    /// </summary>
    public static class SecretsFactory
    {
        static readonly string[] Keys = { SecretKeys.MetaApiKey, SecretKeys.WitClientToken, SecretKeys.WitAppId };

        /// <param name="embeddedJson">What a keyed build embedded; empty in every other build.</param>
        public static ISecretsProvider Create(AppPaths paths, string embeddedJson = null)
        {
#if UNITY_EDITOR
            return new DotEnvSecretsProvider(AppPaths.EditorDotEnv);
#else
            return JsonFileSecretsProvider.ForDevice(paths.DeviceSecrets, embeddedJson);
#endif
        }

        /// <summary>The values RedactingLog masks; never logged themselves.</summary>
        public static string[] ValuesToRedact(ISecretsProvider secrets)
        {
            var values = new string[Keys.Length];
            for (var i = 0; i < Keys.Length; i++)
                values[i] = secrets.TryGet(Keys[i], out var value) ? value : null;
            return values;
        }
    }
}
