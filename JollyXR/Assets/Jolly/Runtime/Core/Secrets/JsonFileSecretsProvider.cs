using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Jolly.Core.Secrets
{
    /// <summary>
    /// Device secrets from <c>persistentDataPath/secrets.json</c>, a flat object of string values pushed with adb (ADR-0010).
    /// A malformed file yields no keys (the app runs offline with "API key missing") and a value-free <see cref="LoadError"/>.
    /// </summary>
    public sealed class JsonFileSecretsProvider : ParsedSecrets
    {
        public JsonFileSecretsProvider(string path)
        {
            if (!File.Exists(path))
                return;
            try
            {
                Parse(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                LoadError = $"secrets file is not a flat JSON object of strings ({e.GetType().Name})";
            }
        }

        /// <summary>Null when the file was absent or parsed cleanly. Never contains secret values.</summary>
        public string LoadError { get; }

        void Parse(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json));
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                throw new JsonReaderException("root is not an object");

            var pending = new List<(string, string)>();
            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                var key = (string)reader.Value;
                if (!reader.Read())
                    break;
                if (reader.TokenType == JsonToken.String)
                    pending.Add((key, (string)reader.Value));
                else
                    reader.Skip();
            }
            if (reader.TokenType != JsonToken.EndObject)
                throw new JsonReaderException("unterminated object");

            foreach (var (key, value) in pending)
                Set(key, value);
        }
    }
}
