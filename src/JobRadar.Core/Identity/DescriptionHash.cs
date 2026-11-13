using System.Security.Cryptography;
using System.Text;

namespace JobRadar.Core.Identity;

/// <summary>Content hash of an offer description, insensitive to whitespace layout and Unicode form (SPEC §6.1).</summary>
public static class DescriptionHash
{
    /// <summary>
    /// Lowercase hex of the full SHA-256 of the text after trimming, collapsing each whitespace run
    /// (any <see cref="char.IsWhiteSpace(char)"/> sequence) to one space, and Unicode NFC normalisation (UTF-8 bytes).
    /// </summary>
    public static string Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalised = CollapseWhitespace(text).Normalize(NormalizationForm.FormC);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalised));
        return Convert.ToHexStringLower(hash);
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace && sb.Length > 0)
            {
                sb.Append(' ');
            }

            pendingSpace = false;
            sb.Append(c);
        }

        return sb.ToString();
    }
}
