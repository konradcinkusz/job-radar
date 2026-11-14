using System.Net;
using System.Text.RegularExpressions;

namespace JobRadar.Sources.Common;

/// <summary>
/// Converts an HTML job description to plain text (SPEC §6.3: adapters carry text, not markup; COPILOT-SDK §6: descriptions stay text
/// for agents). Greenhouse returns its description entity-escaped (SOURCES §4); that case is handled by the first step.
/// </summary>
public static class HtmlText
{
    private const string EscapedOpen = "&lt;";
    private const string EscapedClose = "&gt;";

    private static readonly Regex ScriptOrStyle = new(
        @"<(script|style)\b[^>]*>.*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex LineBreak = new(
        @"<br\s*/?>|</(p|div|li|h[1-6]|tr)\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ListItemOpen = new(@"<li\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AnyTag = new(@"</?[a-z][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TrailingSpaces = new(@"[ \t]+$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex ThreeOrMoreNewlines = new(@"\n{3,}", RegexOptions.CultureInvariant);

    /// <summary>
    /// Steps, in order: (1) if the input holds entity-escaped markup (an <c>&amp;lt;</c> with a later <c>&amp;gt;</c>), decode entities
    /// once; (2) drop <c>script</c> and <c>style</c> elements with their content; (3) turn <c>br</c> and the closing tags of
    /// <c>p, div, li, h1-h6, tr</c> into line breaks and prefix each <c>li</c> with <c>- </c>; (4) remove the remaining tags;
    /// (5) decode entities again; (6) normalise line endings, turn U+00A0 into a space, trim trailing spaces on each line, collapse
    /// three or more newlines into two, and trim the whole text. A null input throws; an empty string returns an empty string.
    /// </summary>
    public static string ToPlainText(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (html.Length == 0)
        {
            return string.Empty;
        }

        var text = IsEscapedMarkup(html) ? WebUtility.HtmlDecode(html) : html;
        text = ScriptOrStyle.Replace(text, string.Empty);
        text = LineBreak.Replace(text, "\n");
        text = ListItemOpen.Replace(text, "- ");
        text = AnyTag.Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\u00A0', ' ');
        text = TrailingSpaces.Replace(text, string.Empty);
        text = ThreeOrMoreNewlines.Replace(text, "\n\n");
        return text.Trim();
    }

    private static bool IsEscapedMarkup(string html)
    {
        var open = html.IndexOf(EscapedOpen, StringComparison.Ordinal);
        return open >= 0 && html.IndexOf(EscapedClose, open + EscapedOpen.Length, StringComparison.Ordinal) >= 0;
    }
}
