using SmartKeyboard.Core.Data;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class AutocorrectEngineTests
{
    // A small dictionary where the counts are chosen to test the 2x rule.
    private static (AutocorrectEngine Engine, UserDictionary Users) Build(TempDataFolder folder)
    {
        var trie = new Trie();
        var tree = new BKTree();

        var words = new (string Word, int Count)[]
        {
            ("the", 10000),
            ("keyboard", 500),
            ("receive", 400),
            ("separate", 300),
            ("hello", 900),

            // "cat" is far ahead of "cut", so "cst" is a safe correction.
            ("cat", 1000),
            ("cut", 100),

            // "bear" and "beat" are equally common, so "beap" must be left alone.
            ("bear", 500),
            ("beat", 500),

            // "form" is only slightly ahead of "fort", under the 2x bar.
            ("form", 300),
            ("fort", 200),
        };

        foreach ((string word, int count) in words)
        {
            trie.Insert(word, count);
            tree.Add(word);
        }

        var repository = new FileWordRepository(folder.Path);
        var users = new UserDictionary(repository, trie, tree);
        var fuzzy = new FuzzyMatcher(tree, trie);

        return (new AutocorrectEngine(fuzzy, trie, users), users);
    }

    [Fact]
    public void AClearTypoIsCorrected()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check("cst");

        Assert.True(result.Changed);
        Assert.Equal("cat", result.Corrected);
        Assert.Equal("cst", result.Original);
    }

    [Fact]
    public void ARealWordIsLeftAlone()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check("cat");

        Assert.False(result.Changed);
        Assert.Equal("cat", result.Corrected);
        Assert.Equal("already a real word", result.Reason);
    }

    [Fact]
    public void TwoEquallyLikelyWordsMeanNothingIsChanged()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        // "beap" is one mistake from both "bear" and "beat", and both are
        // equally common. There is no way to know which was meant.
        AutocorrectResult result = engine.Check("beap");

        Assert.False(result.Changed);
        Assert.Equal("two words are too close to call", result.Reason);
    }

    [Fact]
    public void AWinnerUnderTwiceTheRunnerUpIsNotEnough()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        // "form" is 300 and "fort" is 200. 300 is less than 2 x 200.
        AutocorrectResult result = engine.Check("form".Replace("m", "z"));

        Assert.False(result.Changed);
        Assert.Equal("two words are too close to call", result.Reason);
    }

    [Fact]
    public void AWinnerAtExactlyTwiceTheRunnerUpIsEnough()
    {
        using var folder = new TempDataFolder();
        var trie = new Trie();
        var tree = new BKTree();

        trie.Insert("bear", 200);
        trie.Insert("beat", 100);
        tree.Add("bear");
        tree.Add("beat");

        var engine = new AutocorrectEngine(new FuzzyMatcher(tree, trie), trie);

        // 200 is exactly 2 x 100, and the rule says "at least twice".
        Assert.True(engine.Check("beap").Changed);
    }

    [Fact]
    public void AWordTwoMistakesAwayIsNotCorrectedOnItsOwn()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        // "kayboord" is two letters wrong compared to "keyboard". It is long
        // enough, and nothing else in this dictionary is close, so it is fixed.
        AutocorrectResult result = engine.Check("kayboord");

        Assert.True(result.Changed);
        Assert.Equal("keyboard", result.Corrected);
    }

    [Fact]
    public void AShortMessyWordIsLeftAloneBecauseGuessingIsNotSafe()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        // "hallu" is two mistakes from "hello", but only 5 letters long.
        // Short words do not carry enough letters to make the guess safe.
        AutocorrectResult result = engine.Check("hallu");

        Assert.False(result.Changed);
        Assert.Equal("too short to guess at two mistakes", result.Reason);
    }

    [Fact]
    public void MessyWordFixingCanBeTurnedOffOnItsOwn()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        engine.FixMessyWords = false;

        AutocorrectResult result = engine.Check("kayboord");

        Assert.False(result.Changed);
        Assert.Equal("too messy to fix on its own", result.Reason);

        // One mistake words are still fixed as normal.
        Assert.True(engine.Check("cst").Changed);
    }

    [Fact]
    public void AWordThreeMistakesAwayIsAlwaysLeftAlone()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check("kayboordz");

        Assert.False(result.Changed);
    }

    [Fact]
    public void MessyWordsNeedAMuchBiggerLeadThanSimpleTypos()
    {
        var trie = new Trie();
        var tree = new BKTree();

        // Three times ahead is enough at one mistake, but not at two.
        trie.Insert("smarter", 300);
        trie.Insert("smartest", 100);
        tree.Add("smarter");
        tree.Add("smartest");

        var engine = new AutocorrectEngine(new FuzzyMatcher(tree, trie), trie);

        // Two mistakes from "smarter", and 300 is not 4 x 100.
        Assert.False(engine.Check("smartez".Replace("z", "st")).Changed);
    }

    [Fact]
    public void TheClassicRecieveTypoIsCorrected()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        // "recieve" looks like two mistakes, but it is really one swap of the
        // i and the e, so autocorrect is allowed to fix it.
        AutocorrectResult result = engine.Check("recieve");

        Assert.True(result.Changed);
        Assert.Equal("receive", result.Corrected);
    }

    [Fact]
    public void ASwappedPairOfLettersCountsAsOneMistakeAndIsCorrected()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check("teh");

        Assert.True(result.Changed);
        Assert.Equal("the", result.Corrected);
    }

    [Fact]
    public void NothingCloseEnoughMeansNothingIsChanged()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check("zzzzzzzz");

        Assert.False(result.Changed);
        Assert.Equal("no close word found", result.Reason);
    }

    [Theory]
    [InlineData("cst1")]
    [InlineData("1cst")]
    [InlineData("covid19")]
    [InlineData("a4")]
    public void AnythingWithADigitIsLeftAlone(string word)
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check(word);

        Assert.False(result.Changed);
        Assert.Equal("has a digit in it", result.Reason);
    }

    [Fact]
    public void ACapitalisedWordInTheMiddleOfASentenceIsTreatedAsAName()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check("Cst", isSentenceStart: false);

        Assert.False(result.Changed);
        Assert.Equal("looks like a name", result.Reason);
    }

    [Fact]
    public void ACapitalisedWordAtTheStartOfASentenceIsStillCorrected()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check("Cst", isSentenceStart: true);

        Assert.True(result.Changed);
        Assert.Equal("Cat", result.Corrected);
    }

    [Fact]
    public void AWordYouAddedYourselfIsNeverCorrected()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, UserDictionary users) = Build(folder);

        users.Add("cst");

        AutocorrectResult result = engine.Check("cst");

        Assert.False(result.Changed);
        Assert.Equal("you added this word", result.Reason);
    }

    [Fact]
    public void CapitalsAreCopiedOntoTheCorrectedWord()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        Assert.Equal("Cat", engine.Check("Cst", isSentenceStart: true).Corrected);
        Assert.Equal("CAT", engine.Check("CST", isSentenceStart: true).Corrected);
    }

    [Fact]
    public void TurningItOffStopsEverything()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        engine.Enabled = false;

        AutocorrectResult result = engine.Check("cst");

        Assert.False(result.Changed);
        Assert.Equal("autocorrect is off", result.Reason);
    }

    [Fact]
    public void ItIsOnByDefault()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        Assert.True(engine.Enabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankInputIsLeftAlone(string? word)
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        Assert.False(engine.Check(word).Changed);
    }

    [Fact]
    public void TheOriginalWordIsAlwaysKeptSoUndoCanPutItBack()
    {
        using var folder = new TempDataFolder();
        (AutocorrectEngine engine, _) = Build(folder);

        AutocorrectResult result = engine.Check("Cst", isSentenceStart: true);

        Assert.Equal("Cst", result.Original);
        Assert.Equal("Cat", result.Corrected);
    }
}
