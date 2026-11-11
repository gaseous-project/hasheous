using System.Text.RegularExpressions;

namespace hasheous_lib.Tests;

public class LocalisationMigrationTests
{
    [Fact]
    public void EveryCultureRowHasAnExplicitDirection()
    {
        string sql = ReadMigration();
        MatchCollection rows = Regex.Matches(sql, @"^\('(?<id>[^']+)'.*\)[,;]$", RegexOptions.Multiline);
        MatchCollection directions = Regex.Matches(sql, @"^\('(?<id>[^']+)'.*, (?<direction>0|1)\)[,;]$", RegexOptions.Multiline);

        Assert.Equal(851, rows.Count);
        Assert.Equal(rows.Count, directions.Count);
        Assert.Equal(rows.Count, directions.Select(match => match.Groups["id"].Value).Distinct().Count());
        Assert.Contains("`Direction` INT NOT NULL", sql);
    }

    [Theory]
    [InlineData("en", 0)]
    [InlineData("en-AU", 0)]
    [InlineData("ar", 1)]
    [InlineData("he-IL", 1)]
    [InlineData("pa-Arab-PK", 1)]
    [InlineData("pa-Guru-IN", 0)]
    [InlineData("ks-Deva-IN", 0)]
    [InlineData("uz-Arab-AF", 1)]
    public void CultureDirectionIsSeededPerScript(string culture, int expectedDirection)
    {
        string sql = ReadMigration();

        Assert.Matches(
            $@"(?m)^\('{Regex.Escape(culture)}',.* {expectedDirection}\)[,;]$",
            sql);
    }

    private static string ReadMigration()
    {
        using Stream stream = typeof(global::Classes.Database).Assembly.GetManifestResourceStream("hasheous_lib.Schema.hasheous-1045.sql")
            ?? throw new InvalidOperationException("The localisation migration is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
