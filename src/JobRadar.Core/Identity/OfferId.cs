using System.Security.Cryptography;
using System.Text;

namespace JobRadar.Core.Identity;

/// <summary>Stable identity of an offer across runs (SPEC §6.1).</summary>
public static class OfferId
{
    /// <summary>
    /// Lowercase hex of the SHA-256 of <c>source + "\u001f" + sourceId</c> (UTF-8), first 32 characters.
    /// </summary>
    public static string Of(string source, string sourceId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceId);

        var bytes = Encoding.UTF8.GetBytes(source + "\u001f" + sourceId);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash)[..32];
    }
}
