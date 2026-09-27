using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class AutocorrectContextTests
{
    // Builds an engine over a tiny dictionary and a few word pairs.
    // Time O(n) over the words given.
    private static AutocorrectEngine Build(
        (string Word, int Count)[] words, (string First, string Second, int Count)[] pairs)
    {
        var trie = new Trie();
        var tree = new BKTree();
        var bigrams = new BigramIndex();

        foreach ((string word, int count) in words)
        {
            trie.Insert(word, count);
            tree.Add(word);
        }

        foreach ((string first, string second, int count) in pairs)
        {
            bigrams.Add(first, second, count);
        }

        return new AutocorrectEngine(new FuzzyMatcher(tree, trie), trie, users: null, bigrams: bigrams);
    }

    private static readonly (string, int)[] ThanAndThe = { ("the", 1000), ("than", 100) };

    private static readonly (string, string, int)[] MoreThan =
    {
        ("more", "than", 50),
        ("more", "the", 1),
    };

    [Fact]
    public void ThePreviousWordPicksTheWordThatFitsTheSentence()
    {
        // "the" is ten times commoner, but "more the" is not English.
        AutocorrectEngine engine = Build(ThanAndThe, MoreThan);

        AutocorrectResult result = engine.Check("thn", isSentenceStart: false, previousWord: "more");

        Assert.True(result.Changed);
        Assert.Equal("than", result.Corrected);
    }

    [Fact]
    public void WithoutAPreviousWordNothingChanges()
    {
        AutocorrectEngine engine = Build(ThanAndThe, MoreThan);

        AutocorrectResult result = engine.Check("thn", isSentenceStart: true, previousWord: null);

        Assert.Equal("the", result.Corrected);
    }

    [Fact]
    public void APreviousWordTheDataHasNeverSeenChangesNothing()
    {
        AutocorrectEngine engine = Build(ThanAndThe, MoreThan);

        AutocorrectResult result = engine.Check("thn", isSentenceStart: false, previousWord: "zebra");

        Assert.Equal("the", result.Corrected);
    }

    [Fact]
    public void ContextSettlesATieThatFrequencyCannot()
    {
        // "bear" and "beat" are equally common, so on their own it is too
        // close to call. After "polar" it is not.
        AutocorrectEngine engine = Build(
            new[] { ("bear", 100), ("beat", 100) },
            new[] { ("polar", "bear", 20) });

        Assert.False(engine.Check("beap", false, null).Changed);

        AutocorrectResult result = engine.Check("beap", false, "polar");

        Assert.True(result.Changed);
        Assert.Equal("bear", result.Corrected);
    }

    [Fact]
    public void AWordThatFitsBeatsAnUnfinishedOneThatDoesNot()
    {
        // "bak" is the start of "bake", which used to win outright. But
        // "come bake" is not something people write, and "come back" is.
        AutocorrectEngine engine = Build(
            new[] { ("bake", 50), ("back", 5000) },
            new[] { ("come", "back", 300), ("come", "bake", 0) });

        AutocorrectResult result = engine.Check("bak", false, "come");

        Assert.Equal("back", result.Corrected);
    }

    [Fact]
    public void AMissingApostropheStillWinsInContext()
    {
        // The pair data has no contractions in it, so "i can't" never shows
        // up there. That must not hand "i cant" to "can".
        AutocorrectEngine engine = Build(
            new[] { ("can't", 10), ("can", 1000) },
            new[] { ("i", "can", 500) });

        AutocorrectResult result = engine.Check("cant", false, "i");

        Assert.Equal("can't", result.Corrected);
    }

    [Fact]
    public void AStrongSlipStillBeatsAWeakContextLead()
    {
        // "helo" is one doubled letter from "hello". A small edge for "help"
        // after "say" is not enough to throw that away.
        AutocorrectEngine engine = Build(
            new[] { ("hello", 100), ("help", 1000) },
            new[] { ("say", "hello", 10), ("say", "help", 15) });

        AutocorrectResult result = engine.Check("helo", false, "say");

        Assert.Equal("hello", result.Corrected);
    }
}
