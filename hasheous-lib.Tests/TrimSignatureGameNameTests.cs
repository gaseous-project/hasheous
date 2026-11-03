using Classes;

namespace hasheous_lib.Tests;

public class TrimSignatureGameNameTests
{
    [Theory]
    [InlineData("ZZZ(notgame):Lightspan - Creative Camp", "Lightspan - Creative Camp")]
    [InlineData("ZZZ(notgame):Lightspan - Calamity 3", "Lightspan - Calamity 3")]
    [InlineData("zzz(notgame):Lightspan - K9.5 1", "Lightspan - K9.5 1")]
    public void ScreenScraperNonGamePrefixIsRemovedBeforeTheParenthesisTrim(string name, string expected)
    {
        // Without the prefix strip the name is cut at the '(' of "(notgame)" and every
        // ScreenScraper non-game entry trims down to "ZZZ", so they all match a single
        // data object per platform.
        Assert.Equal(expected, Common.TrimSignatureGameName(name));
    }

    [Fact]
    public void ParentheticalSuffixesAreStillRemoved()
    {
        // The trailing space is pre-existing behavior: the original code cut the string at
        // the '(' without trimming afterwards. Asserted so it stays deliberate.
        Assert.Equal("Creative Camp ", Common.TrimSignatureGameName("Creative Camp (USA)"));
    }

    [Theory]
    [InlineData("Some Game v1.2", "Some Game")]
    [InlineData("Some Game Rev 2", "Some Game")]
    public void TrailingVersionAndRevisionNumbersAreStillRemoved(string name, string expected)
    {
        Assert.Equal(expected, Common.TrimSignatureGameName(name));
    }

    [Fact]
    public void NamesWithoutAPrefixOrSuffixAreUnchanged()
    {
        Assert.Equal("Creative Camp", Common.TrimSignatureGameName("Creative Camp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyAndWhitespaceNamesReturnEmpty(string name)
    {
        Assert.Equal(string.Empty, Common.TrimSignatureGameName(name));
    }
}
