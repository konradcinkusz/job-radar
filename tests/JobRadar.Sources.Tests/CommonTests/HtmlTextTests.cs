using JobRadar.Sources.Common;

namespace JobRadar.Sources.Tests.CommonTests;

public sealed class HtmlTextTests
{
    [Fact]
    public void Entity_escaped_html_becomes_text()
    {
        const string input = "&lt;h2&gt;&lt;strong&gt;Who we are&lt;/strong&gt;&lt;/h2&gt;&lt;p&gt;We build &amp;amp; ship.&lt;/p&gt;";

        Assert.Equal("Who we are\nWe build & ship.", HtmlText.ToPlainText(input));
    }

    [Fact]
    public void Plain_html_becomes_text()
    {
        Assert.Equal("One\nTwo", HtmlText.ToPlainText("<p>One</p><p>Two</p>"));
    }

    [Fact]
    public void List_items_become_lines()
    {
        Assert.Equal("- A\n- B", HtmlText.ToPlainText("<ul><li>A</li><li>B</li></ul>"));
    }

    [Fact]
    public void Script_and_style_are_dropped()
    {
        const string input = "<style>p{color:red}</style><p>Keep</p><script>alert('x')</script>Text";

        Assert.Equal("Keep\nText", HtmlText.ToPlainText(input));
    }

    [Fact]
    public void Script_inside_entity_escaped_html_is_dropped()
    {
        const string input = "&lt;script&gt;alert(1)&lt;/script&gt;&lt;p&gt;Ok&lt;/p&gt;";

        Assert.Equal("Ok", HtmlText.ToPlainText(input));
    }

    [Fact]
    public void Entities_are_decoded()
    {
        // &nbsp; becomes U+00A0 and then a normal space; &copy; becomes U+00A9.
        Assert.Equal("Tom & Jerry ©", HtmlText.ToPlainText("Tom &amp; Jerry&nbsp;&copy;"));
    }

    [Fact]
    public void Br_becomes_a_line_break()
    {
        Assert.Equal("A\nB", HtmlText.ToPlainText("A<br>B<br/>"));
    }

    [Fact]
    public void Blank_lines_collapse_to_one()
    {
        // Two empty paragraphs between A and B leave three newlines, which collapse to two.
        Assert.Equal("A\n\nB", HtmlText.ToPlainText("<p>A</p><p></p><p></p><p>B</p>"));
    }

    [Fact]
    public void Text_inside_a_line_is_kept_intact()
    {
        Assert.Equal("Case  Kept   Spaces", HtmlText.ToPlainText("<p>Case  Kept   Spaces</p>"));
    }

    [Fact]
    public void Null_html_throws()
    {
        Assert.Throws<ArgumentNullException>(() => HtmlText.ToPlainText(null!));
    }

    [Fact]
    public void Empty_html_is_empty()
    {
        Assert.Equal(string.Empty, HtmlText.ToPlainText(string.Empty));
    }
}
