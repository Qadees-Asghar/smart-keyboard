using SmartKeyboard.Core;
using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Tests.DataStructures;

public class TrieTests
{
    private static Trie BuildSample()
    {
        var trie = new Trie();
        trie.Insert("cat", 50);
        trie.Insert("car", 30);
        trie.Insert("care", 20);
        trie.Insert("dog", 10);
        return trie;
    }

    [Fact]
    public void Contains_FindsInsertedWord()
    {
        Trie trie = BuildSample();

        Assert.True(trie.Contains("cat"));
        Assert.True(trie.Contains("dog"));
    }

    [Fact]
    public void Contains_IsFalseForWordThatWasNotInserted()
    {
        Trie trie = BuildSample();

        Assert.False(trie.Contains("cow"));
    }

    [Fact]
    public void Contains_IsFalseForPrefixThatIsNotAWord()
    {
        Trie trie = BuildSample();

        Assert.False(trie.Contains("ca"));
        Assert.True(trie.StartsWith("ca"));
    }

    [Fact]
    public void Contains_IgnoresCaseAndSpaces()
    {
        var trie = new Trie();
        trie.Insert("  HeLLo  ", 5);

        Assert.True(trie.Contains("hello"));
        Assert.True(trie.Contains("HELLO"));
        Assert.Equal(5, trie.GetFrequency("Hello"));
    }

    [Fact]
    public void Insert_IgnoresNullAndEmptyWords()
    {
        var trie = new Trie();
        trie.Insert(null, 5);
        trie.Insert(string.Empty, 5);
        trie.Insert("   ", 5);

        Assert.Equal(0, trie.WordCount);
        Assert.False(trie.Contains(string.Empty));
    }

    [Fact]
    public void GetFrequency_ReturnsStoredCount()
    {
        Trie trie = BuildSample();

        Assert.Equal(50, trie.GetFrequency("cat"));
        Assert.Equal(0, trie.GetFrequency("cow"));
    }

    [Fact]
    public void Insert_SameWordTwice_AddsUpTheFrequency()
    {
        var trie = new Trie();
        trie.Insert("test", 3);
        trie.Insert("test", 4);

        Assert.Equal(7, trie.GetFrequency("test"));
        Assert.Equal(1, trie.WordCount);
        Assert.Equal(7, trie.TotalFrequency);
    }

    [Fact]
    public void SetFrequency_ReplacesTheCountInsteadOfAdding()
    {
        var trie = new Trie();
        trie.Insert("test", 3);
        trie.SetFrequency("test", 10);

        Assert.Equal(10, trie.GetFrequency("test"));
        Assert.Equal(10, trie.TotalFrequency);
    }

    [Fact]
    public void GetWordsWithPrefix_ReturnsEveryMatch()
    {
        Trie trie = BuildSample();

        List<string> words = trie.GetWordsWithPrefix("ca").Select(w => w.Word).OrderBy(w => w).ToList();

        Assert.Equal(new[] { "car", "care", "cat" }, words);
    }

    [Fact]
    public void GetWordsWithPrefix_KeepsTheFrequencyOfEachWord()
    {
        Trie trie = BuildSample();

        WordEntry care = trie.GetWordsWithPrefix("care").Single();

        Assert.Equal("care", care.Word);
        Assert.Equal(20, care.Frequency);
    }

    [Fact]
    public void GetWordsWithPrefix_ReturnsEmptyListForUnknownPrefix()
    {
        Trie trie = BuildSample();

        Assert.Empty(trie.GetWordsWithPrefix("zz"));
    }

    [Fact]
    public void GetWordsWithPrefix_EmptyPrefixReturnsEverything()
    {
        Trie trie = BuildSample();

        Assert.Equal(4, trie.GetWordsWithPrefix(string.Empty).Count);
    }

    [Fact]
    public void GetAllWords_ReturnsEveryStoredWord()
    {
        Trie trie = BuildSample();

        List<string> words = trie.GetAllWords().Select(w => w.Word).OrderBy(w => w).ToList();

        Assert.Equal(new[] { "car", "care", "cat", "dog" }, words);
    }

    [Fact]
    public void WordCount_CountsDistinctWordsOnly()
    {
        Trie trie = BuildSample();

        Assert.Equal(4, trie.WordCount);
        Assert.Equal(110, trie.TotalFrequency);
    }

    [Fact]
    public void Remove_DeletesTheWordButKeepsWordsThatShareLetters()
    {
        Trie trie = BuildSample();

        Assert.True(trie.Remove("care"));

        Assert.False(trie.Contains("care"));
        Assert.True(trie.Contains("car"));
        Assert.Equal(3, trie.WordCount);
        Assert.Equal(90, trie.TotalFrequency);
    }

    [Fact]
    public void Remove_ReturnsFalseForWordThatIsNotThere()
    {
        Trie trie = BuildSample();

        Assert.False(trie.Remove("cow"));
        Assert.Equal(4, trie.WordCount);
    }

    [Fact]
    public void Remove_ThenPrefixSearchNoLongerReturnsTheWord()
    {
        Trie trie = BuildSample();
        trie.Remove("cat");

        List<string> words = trie.GetWordsWithPrefix("ca").Select(w => w.Word).OrderBy(w => w).ToList();

        Assert.Equal(new[] { "car", "care" }, words);
    }
}
