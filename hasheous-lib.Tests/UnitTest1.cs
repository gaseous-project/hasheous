using hasheous_server.Classes;

namespace hasheous_lib.Tests;

public class GetSearchCandidatesTests
{
    private static List<string> GetCandidates(string name)
    {
        return DataObjects.GetSearchCandidates(name);
    }

    [Theory]
    [InlineData("The Legend of Zelda", "The Legend of Zelda", "Legend of Zelda", "Legend of Zelda, The")]
    [InlineData("Legend of Zelda, The", "Legend of Zelda, The", "Legend of Zelda", "The Legend of Zelda")]
    [InlineData("Final Fantasy IV", "Final Fantasy IV", "Final Fantasy 4", "Final Fantasy IV")]
    [InlineData("Resident Evil - Code: Veronica", "Resident Evil - Code: Veronica", "Resident Evil: Code: Veronica", "Resident Evil - Code: Veronica")]
    [InlineData("Sonic (USA)", "Sonic (USA)", "Sonic", "Sonic (USA)")]
    [InlineData("Mega Man v1.2", "Mega Man v1.2", "Mega Man", "Mega Man v1.2")]
    [InlineData("Street Fighter Rev A", "Street Fighter Rev A", "Street Fighter", "Street Fighter Rev A")]
    public void GeneratesExpectedCandidates(string input, string expected1, string expected2, string expected3)
    {
        List<string> candidates = GetCandidates(input);

        Assert.Contains(expected1, candidates);
        Assert.Contains(expected2, candidates);
        Assert.Contains(expected3, candidates);
    }

    [Fact]
    public void ReturnsEmptyListForBlankName()
    {
        List<string> candidates = GetCandidates("   ");
        Assert.Empty(candidates);
    }

    [Theory]
    [InlineData("Resident Evil - Code: Veronica", "Resident Evil Code: Veronica")]
    [InlineData("Metal Gear Solid: The Twin Snakes", "Metal Gear Solid The Twin Snakes")]
    [InlineData("Prince of Persia - The Sands of Time", "Prince of Persia The Sands of Time")]
    public void DropsDelimitersCorrectly(string input, string expectedWithDelimiterDrop)
    {
        List<string> candidates = GetCandidates(input);

        Assert.Contains(expectedWithDelimiterDrop, candidates);
    }

    [Theory]
    [InlineData("Game 1", "Game One")]
    [InlineData("Mega Man 2", "Mega Man Two")]
    [InlineData("Final Fantasy VII", "Final Fantasy VII", "Final Fantasy 7", "Final Fantasy Seven")]
    [InlineData("Take 2", "Take Two")]
    [InlineData("Top 10", "Top Ten")]
    [InlineData("The Room 3", "The Room Three")]
    public void ConvertsNumbersToWords(string input, params string[] expectedCandidates)
    {
        List<string> candidates = GetCandidates(input);

        foreach (string expected in expectedCandidates)
        {
            Assert.Contains(expected, candidates);
        }
    }

    [Theory]
    [InlineData("Game One", "Game 1")]
    [InlineData("Mega Man Two", "Mega Man 2")]
    [InlineData("Final Fantasy Seven", "Final Fantasy 7")]
    [InlineData("Take Twenty One", "Take 21")]
    [InlineData("Top Ten", "Top 10")]
    [InlineData("The Room Three", "The Room 3")]
    public void ConvertsWordsToNumbers(string input, string expectedCandidate)
    {
        List<string> candidates = GetCandidates(input);

        Assert.Contains(expectedCandidate, candidates);
    }

    [Theory]
    [InlineData("Resident Evil 5", "Resident Evil Five")]
    [InlineData("Portal 2", "Portal Two")]
    [InlineData("Call of Duty Modern Warfare 3", "Call of Duty Modern Warfare Three")]
    public void BidirectionalNumberConversion(string input, string expectedCandidate)
    {
        List<string> candidates = GetCandidates(input);

        Assert.Contains(expectedCandidate, candidates);
    }

    [Theory]
    [InlineData("Star Wars: Episode 1 - Racer", "Star Wars: Episode I - Racer")]
    [InlineData("Game 2", "Game II")]
    [InlineData("Final Fantasy 7", "Final Fantasy VII")]
    [InlineData("Chapter 3", "Chapter III")]
    [InlineData("Volume 5", "Volume V")]
    [InlineData("Part 10", "Part X")]
    public void ConvertsNumbersToRomanNumerals(string input, string expectedCandidate)
    {
        List<string> candidates = GetCandidates(input);

        Assert.Contains(expectedCandidate, candidates);
    }

    [Theory]
    [InlineData("Star Wars: Episode I - Racer", "Star Wars: Episode 1 - Racer")]
    [InlineData("Game II", "Game 2")]
    [InlineData("Final Fantasy VII", "Final Fantasy 7")]
    [InlineData("Chapter III", "Chapter 3")]
    [InlineData("Volume V", "Volume 5")]
    [InlineData("Part X", "Part 10")]
    public void ConvertsRomanNumeralsToNumbers(string input, string expectedCandidate)
    {
        List<string> candidates = GetCandidates(input);

        Assert.Contains(expectedCandidate, candidates);
    }
}