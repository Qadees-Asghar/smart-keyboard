using SmartKeyboard.Core;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class SuggestionEngineTests
{
    private static SuggestionEngine BuildEngine()
    {
        var trie = new Trie();
        trie.Insert("the", 1000);
        trie.Insert("they", 500);
        trie.Insert("them", 400);
        trie.Insert("there", 300);
        trie.Insert("these", 200);
        trie.Insert("theory", 100);
        trie.Insert("dog", 50);
        return new SuggestionEngine(trie);
    }

    [Fact]
    public void GetSuggestions_ReturnsTheMostCommonWordsFirst()
    {
        SuggestionEngine engine = BuildEngine();

        List<string> words = engine.GetSuggestionWords("the");

        Assert.Equal(new[] { "the", "they", "them", "there", "these" }, words);
    }

    [Fact]
    public void GetSuggestions_ReturnsAtMostFiveByDefault()
    {
        SuggestionEngine engine = BuildEngine();

        Assert.Equal(5, engine.GetSuggestions("the").Count);
    }

    [Fact]
    public void GetSuggestions_RespectsARequestedCount()
    {
        SuggestionEngine engine = BuildEngine();

        Assert.Equal(2, engine.GetSuggestions("the", null, 2).Count);
    }

    [Fact]
    public void GetSuggestions_ReturnsEmptyForUnknownPrefix()
    {
        SuggestionEngine engine = BuildEngine();

        Assert.Empty(engine.GetSuggestions("zzz"));
    }

    [Fact]
    public void GetSuggestions_ReturnsEmptyForBlankPrefix()
    {
        SuggestionEngine engine = BuildEngine();

        Assert.Empty(engine.GetSuggestions(null));
        Assert.Empty(engine.GetSuggestions(string.Empty));
        Assert.Empty(engine.GetSuggestions("   "));
    }

    [Fact]
    public void GetSuggestions_IncludesTheWholeWordWhenItIsAlsoAPrefix()
    {
        SuggestionEngine engine = BuildEngine();

        Assert.Contains("the", engine.GetSuggestionWords("the"));
    }

    [Fact]
    public void GetSuggestions_IgnoresCase()
    {
        SuggestionEngine engine = BuildEngine();

        Assert.Equal(engine.GetSuggestionWords("the"), engine.GetSuggestionWords("THE"));
    }

    [Fact]
    public void GetMostFrequentWords_ReturnsTheTopWordsOfTheWholeDictionary()
    {
        SuggestionEngine engine = BuildEngine();

        List<string> words = engine.GetMostFrequentWords(3).Select(w => w.Word).ToList();

        Assert.Equal(new[] { "the", "they", "them" }, words);
    }

    [Fact]
    public void Ranking_CanBeSwappedToChangeTheOrder()
    {
        // Its own word list, with counts close enough together that the
        // completion floor drops nothing, so this test is only about the
        // ranking strategy and not about filtering.
        var trie = new Trie();
        trie.Insert("the", 1000);
        trie.Insert("they", 900);
        trie.Insert("their", 800);
        trie.Insert("theatre", 700);

        var engine = new SuggestionEngine(trie) { Ranking = new ShortestWordFirstRanking() };

        List<string> words = engine.GetSuggestionWords("the", null, 4);

        Assert.Equal(new[] { "the", "they", "their", "theatre" }, words);
    }

    [Fact]
    public void GetSuggestions_DropsLongWordsFarRarerThanTheWordAlreadyTyped()
    {
        var trie = new Trie();
        trie.Insert("and", 112359);
        trie.Insert("andrew", 263);
        trie.Insert("anderson", 209);

        var engine = new SuggestionEngine(trie);

        // "and" is over four hundred times commoner than any name starting
        // with it. Nobody typing "and" wanted "anderson", so once the typed
        // word is itself a word, only a realistic alternative is offered.
        List<string> words = engine.GetSuggestionWords("and");

        Assert.Equal(new[] { "and" }, words);
    }

    [Fact]
    public void GetSuggestions_KeepsALongWordThatIsStillPlausible()
    {
        var trie = new Trie();
        trie.Insert("how", 4943);
        trie.Insert("however", 1417);
        trie.Insert("howard", 196);

        var engine = new SuggestionEngine(trie);

        List<string> words = engine.GetSuggestionWords("how");

        // "however" is well over a fifth as common as "how", so it stays.
        // "howard" is not, so it goes.
        Assert.Equal(new[] { "how", "however" }, words);
    }

    [Fact]
    public void GetSuggestions_FiltersNothingWhenTheTypedPrefixIsNotYetAWord()
    {
        var trie = new Trie();
        trie.Insert("help", 5282);
        trie.Insert("hello", 285);
        trie.Insert("helpful", 60);

        var engine = new SuggestionEngine(trie);

        // "hel" is not a word, so there is nothing to compare against and
        // every completion is offered. Finishing the word is the whole point.
        List<string> words = engine.GetSuggestionWords("hel");

        Assert.Equal(3, words.Count);
        Assert.Contains("helpful", words);
    }

    // A tiny strategy used only by the test above, to prove the engine does not
    // care which scoring rule it is given.
    private class ShortestWordFirstRanking : IRankingStrategy
    {
        public double Score(WordEntry word, string? previousWord) => -word.Word.Length;
    }

    [Fact]
    public void GetSuggestions_OffersAContractionTypedWithoutItsApostrophe()
    {
        var trie = new Trie();
        trie.Insert("can't", 208);
        trie.Insert("canterbury", 90);
        trie.Insert("canton", 60);
        trie.Insert("cantor", 20);

        var engine = new SuggestionEngine(trie);

        // The apostrophe sits in the middle of "can't", so walking the Trie
        // from "cant" never reaches it and goes off down the place names
        // instead. Every letter of "can't" was typed, so it leads the list.
        List<string> words = engine.GetSuggestionWords("cant");

        Assert.Equal("can't", words[0]);
        Assert.Contains("canterbury", words);
    }

    [Fact]
    public void GetSuggestions_LeavesAWordThatAlreadyHasAnApostropheAlone()
    {
        var trie = new Trie();
        trie.Insert("can't", 208);

        var engine = new SuggestionEngine(trie);

        Assert.Equal(new[] { "can't" }, engine.GetSuggestionWords("can't"));
    }

    [Fact]
    public void GetSuggestions_StillWorksWhenOnlyTheContractionMatches()
    {
        var trie = new Trie();
        trie.Insert("couldn't", 88);

        var engine = new SuggestionEngine(trie);

        // Nothing in the Trie starts with "couldn", so the ordinary search
        // finds nothing at all and the contraction is the whole answer.
        Assert.Equal(new[] { "couldn't" }, engine.GetSuggestionWords("couldn"));
    }
}
