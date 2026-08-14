using Classes;

namespace hasheous_lib.Tests;

public class SplitSearchTermsTests
{
    [Theory]
    [InlineData("dynasty warriors 4", "4")]
    [InlineData("street fighter ii", "ii")]
    [InlineData("f-zero", "f")]
    [InlineData("final fantasy x", "x")]
    public void ShortWordsAreKeptOutOfTheFullTextQuery(string search, string shortWord)
    {
        Common.SearchTerms terms = Common.SplitSearchTerms(search);

        // a word this short is absent from the full text index, so requiring it in a
        // boolean mode query would match nothing at all
        Assert.Contains(shortWord, terms.UnindexedWords);
        Assert.DoesNotContain(shortWord, terms.IndexedWords);
        Assert.DoesNotContain(shortWord, terms.BooleanQuery);
    }

    [Theory]
    [InlineData("the legend of zelda", "the")]
    [InlineData("call of duty", "of")]
    [InlineData("where in the world", "where")]
    public void StopWordsAreKeptOutOfTheFullTextQuery(string search, string stopWord)
    {
        Common.SearchTerms terms = Common.SplitSearchTerms(search);

        // stopwords are skipped when the index is built, so they behave like short words
        Assert.Contains(stopWord, terms.UnindexedWords);
        Assert.DoesNotContain(stopWord, terms.IndexedWords);
    }

    [Fact]
    public void IndexedWordsBecomeRequiredPrefixTerms()
    {
        Common.SearchTerms terms = Common.SplitSearchTerms("dynasty warriors 4");

        Assert.Equal(new List<string> { "dynasty", "warriors" }, terms.IndexedWords);
        Assert.Equal("+dynasty* +warriors*", terms.BooleanQuery);
    }

    [Fact]
    public void PunctuationSeparatesWords()
    {
        Common.SearchTerms terms = Common.SplitSearchTerms("Resident Evil - Code: Veronica");

        Assert.Equal(new List<string> { "Resident", "Evil", "Code", "Veronica" }, terms.IndexedWords);
        Assert.Empty(terms.UnindexedWords);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-- ??? --")]
    public void ReturnsNoWordsForUnsearchableInput(string? search)
    {
        Common.SearchTerms terms = Common.SplitSearchTerms(search);

        Assert.False(terms.HasWords);
        Assert.Equal("", terms.BooleanQuery);
    }
}

public class BuildNameSearchPredicateTests
{
    [Fact]
    public void CombinesFullTextWithARegexForEachUnindexedWord()
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>();

        string? predicate = Common.BuildNameSearchPredicate("Name", "dynasty warriors 4", parameters);

        Assert.Equal("(MATCH(`Name`) AGAINST(@search_fulltext IN BOOLEAN MODE) AND `Name` RLIKE @search_unindexed0)", predicate);
        Assert.Equal("+dynasty* +warriors*", parameters["search_fulltext"]);
        Assert.Equal("\\b4", parameters["search_unindexed0"]);
    }

    [Fact]
    public void FallsBackToRegexOnlyWhenNoWordIsIndexed()
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>();

        string? predicate = Common.BuildNameSearchPredicate("Name", "the x", parameters);

        Assert.Equal("(`Name` RLIKE @search_unindexed0 AND `Name` RLIKE @search_unindexed1)", predicate);
        Assert.False(parameters.ContainsKey("search_fulltext"));
    }

    [Fact]
    public void ReturnsNullWhenThereIsNothingToSearchFor()
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>();

        Assert.Null(Common.BuildNameSearchPredicate("Name", "!!!", parameters));
        Assert.Empty(parameters);
    }

    [Fact]
    public void ParameterPrefixKeepsParametersUnique()
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>();

        string? predicate = Common.BuildNameSearchPredicate("Publisher", "sega 2", parameters, "publisher");

        Assert.Equal("(MATCH(`Publisher`) AGAINST(@publisher_fulltext IN BOOLEAN MODE) AND `Publisher` RLIKE @publisher_unindexed0)", predicate);
    }
}

public class BuildNameRelevanceOrderByTests
{
    [Fact]
    public void RanksExactThenPrefixThenContains()
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>();

        string orderBy = Common.BuildNameRelevanceOrderBy("Name", " dynasty warriors 4 ", parameters);

        Assert.Equal("CASE WHEN `Name` = @search_exact THEN 0 WHEN `Name` LIKE @search_prefix THEN 1 WHEN `Name` LIKE @search_contains THEN 2 ELSE 3 END, CHAR_LENGTH(`Name`), `Name`", orderBy);
        Assert.Equal("dynasty warriors 4", parameters["search_exact"]);
        Assert.Equal("dynasty warriors 4%", parameters["search_prefix"]);
        Assert.Equal("%dynasty warriors 4%", parameters["search_contains"]);
    }

    [Fact]
    public void EscapesLikeWildcardsInTheSearchString()
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>();

        Common.BuildNameRelevanceOrderBy("Name", "100% Orange_Juice", parameters);

        Assert.Equal("100\\% Orange\\_Juice%", parameters["search_prefix"]);
    }

    [Fact]
    public void FallsBackToNameOrderWhenThereIsNoSearchString()
    {
        Dictionary<string, object> parameters = new Dictionary<string, object>();

        Assert.Equal("`Name`", Common.BuildNameRelevanceOrderBy("Name", "   ", parameters));
        Assert.Empty(parameters);
    }
}
