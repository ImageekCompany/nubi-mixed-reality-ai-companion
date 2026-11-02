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
            if (File.Exists(path))
                ParseText(File.ReadAllText(path));
        }

        JsonFileSecretsProvider() { }

        /// <summary>The pushed file, else the JSON a keyed build embedded (ADR-0049): a pushed file can replace a revoked key.</summary>
        public static JsonFileSecretsProvider ForDevice(string path, string embeddedJson)
        {
            if (File.Exists(path) || string.IsNullOrEmpty(embeddedJson))
                return new JsonFileSecretsProvider(path);
            var embedded = new JsonFileSecretsProvider();
            embedded.ParseText(embeddedJson);
            return embedded;
        }

        /// <summary>Null when the file was absent or parsed cleanly. Never contains secret values.</summary>
        public string LoadError { get; private set; }

        void ParseText(string json)
        {
            try
            {
                Parse(json);
            }
            catch (JsonException e)
            {
                LoadError = $"secrets file is not a flat JSON object of strings ({e.GetType().Name})";
            }
        }

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
