using JobRadar.Core.Identity;

namespace JobRadar.Core.Tests;

public class NormalizerTests
{
    [Theory]
    [InlineData("Acme Sp. z o.o.", "ACME")]
    [InlineData("Zürich Tech GmbH", "zurich tech")]
    [InlineData("Zürich Tech GmbH", "zurich tech")]
    [InlineData("Acme S.A.", "acme")]
    [InlineData("Acme Spółka", "acme")]
    [InlineData("Foo Ltd.", "FOO")]
    [InlineData("Bar LLC", "bar")]
    [InlineData("Baz Inc.", "baz")]
    [InlineData("Qux S.L.", "qux")]
    [InlineData("Quux S.A.U.", "quux")]
    [InlineData("Corge B.V.", "corge")]
    [InlineData("Grault Oy", "grault")]
    [InlineData("Garply AB", "garply")]
    [InlineData("Waldo AG", "waldo")]
    [InlineData("Fred SAS", "fred")]
    [InlineData("Plugh SRL", "plugh")]
    [InlineData("Xyzzy AS", "xyzzy")]
    public void Company_equivalent_forms_match(string written, string other)
    {
        Assert.Equal(Normalizer.Company(other), Normalizer.Company(written));
    }

    [Theory]
    [InlineData("Aerospace", "aerospace")]
    [InlineData("Ablecom", "ablecom")]
    [InlineData("Oyster", "oyster")]
    [InlineData("Asteria", "asteria")]
    [InlineData("Incognito", "incognito")]
    [InlineData("Agency Labs", "agency labs")]
    public void Company_short_legal_words_are_removed_only_as_whole_tokens(string input, string expected)
    {
        Assert.Equal(expected, Normalizer.Company(input));
    }

    [Theory]
    [InlineData("Acme", "Acme Labs")]
    [InlineData("Zurich Tech", "Zurich Bank")]
    [InlineData("Ablecom", "Ablecom Ltd Labs")]
    public void Company_different_companies_do_not_match(string a, string b)
    {
        Assert.NotEqual(Normalizer.Company(a), Normalizer.Company(b));
    }

    [Theory]
    [InlineData("Senior .NET Developer (m/f/d)", "senior .net developer")]
    [InlineData("Senior .NET Developer (m/w/d)", "Senior .NET Developer")]
    [InlineData("Backend Engineer (f/m/x)", "backend engineer")]
    [InlineData("Data Analyst (all genders)", "data analyst")]
    [InlineData("Data Analyst m/k", "data analyst")]
    [InlineData("Data Analyst(m/f/d)", "data analyst")]
    [InlineData("Software Engineer, Remote", "Software Engineer Remote")]
    [InlineData("Product Manager (Zürich)", "product manager (Zurich)")]
    [InlineData("Senior  Developer   (m/f/d)", "senior developer")]
    [InlineData("AI Engineer (m/f/d)", "AI Engineer")]
    [InlineData("SENIOR .NET DEVELOPER", "senior .net developer")]
    [InlineData("Full-Stack Developer (m/f/d)", "Full Stack Developer")]
    [InlineData("Data Engineer (Python/SQL) (m/w/d)", "Data Engineer (Python/SQL)")]
    [InlineData("Frontend Developer (Vue.js) (all genders)", "Frontend Developer (Vue.js)")]
    public void Title_equivalent_forms_match(string written, string other)
    {
        Assert.Equal(Normalizer.Title(other), Normalizer.Title(written));
    }

    [Theory]
    [InlineData("Senior .NET Developer", "Junior .NET Developer")]
    [InlineData("Senior Developer", "Lead Developer")]
    [InlineData("Developer", "Senior Developer")]
    [InlineData("AI Engineer", "AI Engineering Manager")]
    [InlineData("Software Engineer", "Engineering Manager")]
    [InlineData("Staff Engineer", "Principal Engineer")]
    public void Title_level_words_are_kept(string a, string b)
    {
        Assert.NotEqual(Normalizer.Title(a), Normalizer.Title(b));
    }

    [Fact]
    public void Title_keeps_level_word_after_removing_gender_marker()
    {
        Assert.Equal("senior net developer", Normalizer.Title("Senior .NET Developer (m/f/d)"));
    }

    [Fact]
    public void Title_does_not_remove_legal_form_words()
    {
        Assert.Equal("acme gmbh engineer", Normalizer.Title("Acme GmbH Engineer"));
    }
}
