using hasheous_server.Classes.Metadata.HowLongToBeat;
using hasheous_server.Classes.MetadataLib;

namespace hasheous_lib.Tests;

public class HowLongToBeatMatchTests
{
    private static HowLongToBeatGame Game(long id, string name, string platforms = "", string type = "game", long completions = 0, string alias = "")
    {
        return new HowLongToBeatGame
        {
            GameId = id,
            GameName = name,
            ProfilePlatform = platforms,
            GameType = type,
            CountComp = completions,
            GameAlias = alias
        };
    }

    [Fact]
    public void PrefersExactNameOverHacksWithLongerNames()
    {
        List<HowLongToBeatGame> games =
        [
            Game(98727, "Super Metroid - GBA Edition", "Game Boy Advance", "hack", 4),
            Game(9390, "Super Metroid", "Super Nintendo", "game", 4755),
            Game(123780, "Super Metroid: Ancient Chozo", "Super Nintendo", "hack", 1)
        ];

        HowLongToBeatGame? match = MetadataHowLongToBeat.SelectBestMatch("Super Metroid", "Super Nintendo Entertainment System", games);

        Assert.NotNull(match);
        Assert.Equal(9390, match.GameId);
    }

    [Fact]
    public void UsesPlatformToBreakTiesBetweenSameNamedGames()
    {
        List<HowLongToBeatGame> games =
        [
            Game(1, "Doom", "PC", "game", 10000),
            Game(2, "Doom", "Super Nintendo", "game", 50)
        ];

        HowLongToBeatGame? match = MetadataHowLongToBeat.SelectBestMatch("Doom", "Super Nintendo Entertainment System", games);

        Assert.NotNull(match);
        Assert.Equal(2, match.GameId);
    }

    [Fact]
    public void FallsBackToMostCompletedWhenPlatformDoesNotDisambiguate()
    {
        List<HowLongToBeatGame> games =
        [
            Game(1, "Doom", "PC", "game", 50),
            Game(2, "Doom", "PC", "game", 10000)
        ];

        HowLongToBeatGame? match = MetadataHowLongToBeat.SelectBestMatch("Doom", null, games);

        Assert.NotNull(match);
        Assert.Equal(2, match.GameId);
    }

    [Fact]
    public void MatchesOnAlias()
    {
        List<HowLongToBeatGame> games =
        [
            Game(10, "Biohazard", "PlayStation", "game", 100, "Resident Evil, BH")
        ];

        HowLongToBeatGame? match = MetadataHowLongToBeat.SelectBestMatch("Resident Evil", "Sony PlayStation", games);

        Assert.NotNull(match);
        Assert.Equal(10, match.GameId);
    }

    [Fact]
    public void ReturnsNullWhenNoResultIsAStrongMatch()
    {
        List<HowLongToBeatGame> games =
        [
            Game(1, "Metroid Prime", "GameCube", "game", 1000)
        ];

        Assert.Null(MetadataHowLongToBeat.SelectBestMatch("Super Mario World", null, games));
        Assert.Null(MetadataHowLongToBeat.SelectBestMatch("Super Mario World", null, new List<HowLongToBeatGame>()));
        Assert.Null(MetadataHowLongToBeat.SelectBestMatch("Super Mario World", null, null));
    }

    [Theory]
    [InlineData("Super Nintendo Entertainment System", "Super Nintendo", true)]
    [InlineData("Sony PlayStation", "PlayStation", true)]
    [InlineData("Nintendo Switch", "PC, Nintendo Switch", true)]
    [InlineData("Sony PlayStation", "PlayStation 2", false)]
    [InlineData("Nintendo 64", "Super Nintendo", false)]
    [InlineData("", "PC", false)]
    [InlineData("PC", "", false)]
    public void MatchesPlatformNames(string platformName, string profilePlatforms, bool expected)
    {
        Assert.Equal(expected, MetadataHowLongToBeat.IsPlatformMatch(platformName, profilePlatforms));
    }
}
