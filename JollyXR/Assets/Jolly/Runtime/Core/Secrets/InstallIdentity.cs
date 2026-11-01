using System;
using System.Security.Cryptography;
using System.Text;
using Jolly.Core.Diagnostics;

namespace Jolly.Core.Secrets
{
    /// <summary>
    /// The Responses <c>safety_identifier</c>: SHA-256 of a random per-install GUID, truncated to 32 hex characters.
    /// Never <c>SystemInfo.deviceUniqueIdentifier</c>, which is stable and hardware-derived (architecture §11).
    /// </summary>
    public static class InstallIdentity
    {
        public static string LoadOrCreateSafetyIdentifier(string path, Func<Guid> newGuid)
        {
            if (!AtomicFile.TryReadAllText(path, out var text) || !Guid.TryParse(text.Trim(), out var guid))
            {
                guid = newGuid();
                AtomicFile.WriteAllText(path, guid.ToString("D"));
            }
            return Hash(guid);
        }

        static string Hash(Guid guid)
        {
            using var sha = SHA256.Create();
            var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(guid.ToString("D")));
            var hex = new StringBuilder(32);
            for (var i = 0; i < 16; i++)
                hex.Append(digest[i].ToString("x2"));
            return hex.ToString();
        }
    }
}
