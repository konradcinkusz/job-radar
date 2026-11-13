using System.Text;
using System.Text.RegularExpressions;
using JobRadar.Core.Identity;

namespace JobRadar.Core.Tests;

public class IdentityTests
{
    // Computed outside this code, with:
    //   printf '%s' 'greenhouse'$'\x1f''123' | sha256sum | cut -c1-32
    private const string GreenhouseVector = "63a5240d764553187ef2d557a0bf64d8";

    [Fact]
    public void OfferId_is_32_lowercase_hex()
    {
        var id = OfferId.Of("greenhouse", "123");

        Assert.Matches(new Regex("^[0-9a-f]{32}$"), id);
    }

    [Fact]
    public void OfferId_differs_for_swapped_arguments()
    {
        var forward = OfferId.Of("greenhouse", "123");
        var swapped = OfferId.Of("123", "greenhouse");

        Assert.NotEqual(forward, swapped);
    }

    [Fact]
    public void OfferId_known_vector()
    {
        Assert.Equal(GreenhouseVector, OfferId.Of("greenhouse", "123"));
    }

    [Fact]
    public void DescriptionHash_ignores_whitespace_differences()
    {
        const string plain = "Senior .NET Developer Warsaw";
        const string messy = "  Senior  .NET\n\tDeveloper\r\n Warsaw  ";

        Assert.NotEqual(plain, messy);
        Assert.Equal(DescriptionHash.Of(plain), DescriptionHash.Of(messy));
    }

    [Fact]
    public void DescriptionHash_changes_when_a_word_changes()
    {
        var senior = DescriptionHash.Of("Senior .NET Developer");
        var junior = DescriptionHash.Of("Junior .NET Developer");

        Assert.NotEqual(senior, junior);
    }

    [Fact]
    public void DescriptionHash_treats_nfc_and_nfd_alike()
    {
        // Precomposed (NFC): U+0144 and U+00E9. Decomposed (NFD): "n" + U+0301 and "e" + U+0301.
        const string nfc = "Gdańsk, café";
        const string nfd = "Gdańsk, café";

        // Guard: the two inputs really differ in code units, so the equality below is not vacuous.
        Assert.NotEqual(nfc, nfd);
        Assert.Equal(nfc, nfd.Normalize(NormalizationForm.FormC));

        Assert.Equal(DescriptionHash.Of(nfc), DescriptionHash.Of(nfd));
    }
}
