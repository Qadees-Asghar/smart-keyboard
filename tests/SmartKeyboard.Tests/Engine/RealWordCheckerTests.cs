using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class RealWordCheckerTests
{
    // A tiny world where "email from the" is common and "email form the" is
    // not, but "different form of" is how people really write.
    private static RealWordChecker Build(bool enabled = true)
    {
        var words = new Trie();
        words.Insert("from", 1000);
        words.Insert("form", 200);
        words.Insert("the", 5000);
        words.Insert("of", 4000);
        words.Insert("email", 100);
        words.Insert("different", 100);

        var bigrams = new BigramIndex();
        bigrams.Add("email", "from", 100);
        bigrams.Add("email", "form", 2);
        bigrams.Add("from", "the", 500);
        bigrams.Add("form", "the", 5);
        bigrams.Add("different", "form", 1);
        bigrams.Add("different", "from", 60);
        bigrams.Add("form", "of", 300);
        bigrams.Add("from", "of", 1);

        return new RealWordChecker(bigrams, words) { Enabled = enabled };
    }

    [Fact]
    public void TheWrongWordBetweenTwoGoodNeighboursIsFixed()
    {
        AutocorrectResult result = Build().Check("email", "form", "the");

        Assert.True(result.Changed);
        Assert.Equal("from", result.Corrected);
    }

    [Fact]
    public void AMixupTheUserHasJustTypedCannotVouchForItself()
    {
        // Learning adds "email form" to the pair data the moment it is typed,
        // heavily weighted, before the check on "form" has run.
        var words = new Trie();
        words.Insert("from", 1000);
        words.Insert("form", 200);
        words.Insert("the", 5000);
        words.Insert("email", 100);

        var bigrams = new BigramIndex();
        bigrams.Add("email", "from", 100);
        bigrams.Add("email", "form", 2);
        bigrams.Add("from", "the", 500);
        bigrams.Add("form", "the", 5);

        var learned = new Dictionary<string, int> { ["email form"] = 400, ["form the"] = 400 };
        bigrams.Add("email", "form", 400);
        bigrams.Add("form", "the", 400);

        var checker = new RealWordChecker(
            bigrams,
            words,
            (a, b) => learned.TryGetValue($"{a} {b}", out int n) ? n : 0) { Enabled = true };

        Assert.Equal("from", checker.Check("email", "form", "the").Corrected);
    }

    [Fact]
    public void TheRightWordIsLeftAlone()
    {
        Assert.False(Build().Check("email", "from", "the").Changed);
    }

    [Fact]
    public void OneSideCannotOutvoteTheOther()
    {
        // "different from" is far commoner than "different form", but
        // "form of" is far commoner than "from of", and it is "a different
        // form of" that was written.
        Assert.False(Build().Check("different", "form", "of").Changed);
    }

    [Fact]
    public void CapitalsAreKept()
    {
        Assert.Equal("From", Build().Check("email", "Form", "the").Corrected);
    }

    [Fact]
    public void NothingHappensWhenSwitchedOff()
    {
        AutocorrectResult result = Build(enabled: false).Check("email", "form", "the");

        Assert.False(result.Changed);
    }

    [Fact]
    public void WordsNobodyMixesUpAreNeverTouched()
    {
        Assert.False(Build().Check("email", "the", "of").Changed);
        Assert.False(RealWordChecker.IsConfusable("email"));
        Assert.True(RealWordChecker.IsConfusable("Form"));
    }

    [Fact]
    public void WithNoNeighboursAtAllThereIsNothingToGoOn()
    {
        Assert.False(Build().Check(null, "form", null).Changed);
    }

    [Fact]
    public void EveryWordInTheListHasItsPartnerAsAnAlternative()
    {
        foreach (string[] set in RealWordChecker.ConfusionSets)
        {
            Assert.True(set.Length >= 2);
            Assert.All(set, word => Assert.True(RealWordChecker.IsConfusable(word)));
            Assert.All(set, word => Assert.DoesNotContain('\'', word));
        }
    }
}
