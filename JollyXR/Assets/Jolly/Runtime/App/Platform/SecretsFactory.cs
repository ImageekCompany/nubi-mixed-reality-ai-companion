using Jolly.Contracts;
using Jolly.Core.Secrets;

namespace Jolly.App.Platform
{
    /// <summary>
    /// The secrets source per platform (ADR-0010): the pushed persistentDataPath/secrets.json on device, the repository
    /// .env in the Editor. Nothing secret is ever serialized into the build.
    /// </summary>
    public static class SecretsFactory
    {
        static readonly string[] Keys = { SecretKeys.MetaApiKey, SecretKeys.WitClientToken, SecretKeys.WitAppId };

        public static ISecretsProvider Create(AppPaths paths)
        {
#if UNITY_EDITOR
            return new DotEnvSecretsProvider(AppPaths.EditorDotEnv);
#else
            return new JsonFileSecretsProvider(paths.DeviceSecrets);
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
