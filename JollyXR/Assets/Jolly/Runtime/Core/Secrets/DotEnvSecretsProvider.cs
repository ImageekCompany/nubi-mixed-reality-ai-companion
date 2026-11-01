using System.IO;

namespace Jolly.Core.Secrets
{
    /// <summary>Editor secrets from <c>&lt;repo&gt;/.env</c> (ADR-0010): KEY=value lines, optional <c>export</c>, quotes, # comments, CRLF.</summary>
    public sealed class DotEnvSecretsProvider : ParsedSecrets
    {
        public DotEnvSecretsProvider(string path)
        {
            if (!File.Exists(path))
                return;
            foreach (var raw in File.ReadAllLines(path))
                ParseLine(raw.Trim());
        }

        void ParseLine(string line)
        {
            if (line.Length == 0 || line[0] == '#')
                return;
            if (line.StartsWith("export "))
                line = line.Substring(7).TrimStart();
            var eq = line.IndexOf('=');
            if (eq <= 0)
                return;
            Set(line.Substring(0, eq).Trim(), ParseValue(line.Substring(eq + 1).Trim()));
        }

        static string ParseValue(string value)
        {
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\''))
            {
                var close = value.IndexOf(value[0], 1);
                if (close > 0)
                    return value.Substring(1, close - 1);
            }
            var comment = value.IndexOf(" #");
            return comment >= 0 ? value.Substring(0, comment).TrimEnd() : value;
        }
    }
}
