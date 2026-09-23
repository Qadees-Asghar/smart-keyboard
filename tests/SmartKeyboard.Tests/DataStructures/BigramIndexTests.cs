using SmartKeyboard.Core;
using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Tests.DataStructures;

public class BigramIndexTests
{
    private static BigramIndex BuildSample()
    {
        var index = new BigramIndex();
        index.Add("good", "morning", 60);
        index.Add("good", "night", 30);
        index.Add("good", "luck", 10);
        index.Add("thank", "you", 100);
        return index;
    }

    [Fact]
    public void GetCount_ReturnsHowOftenOneWordFollowedAnother()
    {
        BigramIndex index = BuildSample();

        Assert.Equal(60, index.GetCount("good", "morning"));
        Assert.Equal(0, index.GetCount("good", "banana"));
        Assert.Equal(0, index.GetCount("unknown", "word"));
    }

    [Fact]
    public void Add_SamePairTwice_AddsUpTheCounts()
    {
        var index = new BigramIndex();
        index.Add("good", "morning", 2);
        index.Add("good", "morning", 3);

        Assert.Equal(5, index.GetCount("good", "morning"));
        Assert.Equal(1, index.PairCount);
    }

    [Fact]
    public void GetTotalAfter_AddsUpEveryPairStartingWithTheWord()
    {
        BigramIndex index = BuildSample();

        Assert.Equal(100, index.GetTotalAfter("good"));
        Assert.Equal(0, index.GetTotalAfter("nothing"));
    }

    [Fact]
    public void GetProbability_DividesThePairCountByTheTotal()
    {
        BigramIndex index = BuildSample();

        Assert.Equal(0.6, index.GetProbability("good", "morning"), 6);
        Assert.Equal(0.3, index.GetProbability("good", "night"), 6);
    }

    [Fact]
    public void GetProbability_IsZeroWhenTheFirstWordWasNeverSeen()
    {
        BigramIndex index = BuildSample();

        Assert.Equal(0.0, index.GetProbability("nothing", "here"));
    }

    [Fact]
    public void GetFollowers_ReturnsEveryWordSeenAfterTheGivenWord()
    {
        BigramIndex index = BuildSample();

        List<string> followers = index.GetFollowers("good").Select(f => f.Word).OrderBy(w => w).ToList();

        Assert.Equal(new[] { "luck", "morning", "night" }, followers);
    }

    [Fact]
    public void GetFollowers_ReturnsEmptyListForAnUnknownWord()
    {
        Assert.Empty(BuildSample().GetFollowers("nothing"));
    }

    [Fact]
    public void Add_IgnoresCase()
    {
        var index = new BigramIndex();
        index.Add("Good", "Morning", 5);

        Assert.Equal(5, index.GetCount("good", "morning"));
        Assert.Equal(5, index.GetCount("GOOD", "MORNING"));
    }

    [Fact]
    public void Add_IgnoresBlankWords()
    {
        var index = new BigramIndex();
        index.Add(null, "morning", 5);
        index.Add("good", "   ", 5);
        index.Add(string.Empty, string.Empty, 5);

        Assert.Equal(0, index.PairCount);
        Assert.Equal(0, index.FirstWordCount);
    }

    [Fact]
    public void HasFollowers_IsTrueOnlyForWordsWeHaveSeenSomethingAfter()
    {
        BigramIndex index = BuildSample();

        Assert.True(index.HasFollowers("good"));
        Assert.False(index.HasFollowers("morning"));
    }
}
