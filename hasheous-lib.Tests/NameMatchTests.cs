using Classes;

namespace hasheous_lib.Tests;

public class NumberAwareNameMatchTests
{
    [Theory]
    [InlineData("Doom", "Doom")]
    [InlineData("Mega Man 2", "Mega Man 2")]
    [InlineData("Super Mario Bros", "Super Mario Bros.")]
    [InlineData("Sonic 2", "Sonic the Hedgehog 2")]
    [InlineData("Grand Theft Auto V", "Grand Theft Auto V ")]
    public void AcceptsGenuineMatches(string candidate, string resultName)
    {
        int score = Common.GetNumberAwareNameMatchScore(candidate, resultName);

        Assert.True(score >= 8, $"Expected '{candidate}' to match '{resultName}', but scored {score}.");
    }

    [Theory]
    [InlineData("Doom", "Doom 3")]
    [InlineData("Doom", "Doom 64")]
    [InlineData("Doom", "Doom II")]
    [InlineData("Grand Theft Auto", "Grand Theft Auto V")]
    [InlineData("Final Fantasy VII", "Final Fantasy VIII")]
    [InlineData("Metroid", "Metroid Prime")]
    [InlineData("Worms", "Worms Armageddon")]
    [InlineData("Doom 3", "Doom")]
    public void RejectsMatchesThatChangeTheNumbering(string candidate, string resultName)
    {
        Assert.Equal(int.MinValue, Common.GetNumberAwareNameMatchScore(candidate, resultName));
    }

    [Theory]
    [InlineData("", "Doom")]
    [InlineData("Doom", "")]
    [InlineData("   ", "Doom")]
    [InlineData("Doom", null)]
    public void RejectsMissingNames(string candidate, string? resultName)
    {
        Assert.Equal(int.MinValue, Common.GetNumberAwareNameMatchScore(candidate, resultName));
    }
}
