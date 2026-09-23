using SmartKeyboard.Core;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class NextWordPredictorTests
{
    private static NextWordPredictor Build()
    {
        var words = new Trie();
        words.Insert("the", 5000);
        words.Insert("and", 4000);
        words.Insert("good", 1000);
        words.Insert("morning", 300);
        words.Insert("night", 200);
        words.Insert("luck", 100);
        words.Insert("idea", 50);
        words.Insert("thank", 400);
        words.Insert("you", 3000);
        words.Insert("lonely", 10);

        var bigrams = new BigramIndex();
        bigrams.Add("good", "morning", 60);
        bigrams.Add("good", "night", 30);
        bigrams.Add("good", "luck", 8);
        bigrams.Add("good", "idea", 2);
        bigrams.Add("thank", "you", 100);

        return new NextWordPredictor(bigrams, words);
    }

    [Fact]
    public void PredictNext_PutsTheMostLikelyWordFirst()
    {
        NextWordPredictor predictor = Build();

        List<string> next = predictor.PredictNextWords("good");

        Assert.Equal("morning", next[0]);
        Assert.Equal("night", next[1]);
        Assert.Equal("luck", next[2]);
        Assert.Equal("idea", next[3]);
    }

    [Fact]
    public void PredictNext_OnlyOffersWordsActuallySeenAfterTheGivenWord()
    {
        NextWordPredictor predictor = Build();

        List<string> next = predictor.PredictNextWords("thank");

        Assert.Equal(new[] { "you" }, next);
    }

    [Fact]
    public void PredictNext_IgnoresCase()
    {
        NextWordPredictor predictor = Build();

        Assert.Equal(predictor.PredictNextWords("good"), predictor.PredictNextWords("GOOD"));
    }

    [Fact]
    public void PredictNext_ReturnsAtMostFiveByDefault()
    {
        NextWordPredictor predictor = Build();

        Assert.True(predictor.PredictNext("good").Count <= 5);
    }

    [Fact]
    public void PredictNext_RespectsARequestedCount()
    {
        NextWordPredictor predictor = Build();

        Assert.Equal(2, predictor.PredictNext("good", 2).Count);
    }

    [Fact]
    public void PredictNext_WithZeroOrLessAsksForNothingAndGetsNothing()
    {
        NextWordPredictor predictor = Build();

        Assert.Empty(predictor.PredictNext("good", 0));
        Assert.Empty(predictor.PredictNext("good", -3));
    }

    [Fact]
    public void GetProbability_DividesThePairCountByTheTotalForThatWord()
    {
        NextWordPredictor predictor = Build();

        // "good" was followed by something 100 times, 60 of them "morning".
        Assert.Equal(0.6, predictor.GetProbability("good", "morning"), 6);
        Assert.Equal(0.3, predictor.GetProbability("good", "night"), 6);
        Assert.Equal(0.0, predictor.GetProbability("good", "banana"), 6);
    }

    [Fact]
    public void GetProbability_IsZeroWhenTheWordHasNoPairsAtAll()
    {
        NextWordPredictor predictor = Build();

        Assert.Equal(0.0, predictor.GetProbability("lonely", "anything"));
    }

    [Fact]
    public void PredictNext_FallsBackToTheMostCommonWordsWhenThereIsNoPairData()
    {
        NextWordPredictor predictor = Build();

        List<string> next = predictor.PredictNextWords("lonely");

        Assert.True(predictor.LastAnswerWasFallback);
        Assert.Equal("the", next[0]);
        Assert.Equal("and", next[1]);
        Assert.Equal("you", next[2]);
    }

    [Fact]
    public void PredictNext_FallsBackWhenThereIsNoPreviousWordAtAll()
    {
        NextWordPredictor predictor = Build();

        Assert.Equal("the", predictor.PredictNextWords(null)[0]);
        Assert.Equal("the", predictor.PredictNextWords(string.Empty)[0]);
        Assert.True(predictor.LastAnswerWasFallback);
    }

    [Fact]
    public void LastAnswerWasFallback_IsFalseWhenRealPairDataWasUsed()
    {
        NextWordPredictor predictor = Build();

        predictor.PredictNextWords("good");

        Assert.False(predictor.LastAnswerWasFallback);
    }

    [Fact]
    public void HasContext_IsTrueOnlyForWordsWeHaveSeenSomethingAfter()
    {
        NextWordPredictor predictor = Build();

        Assert.True(predictor.HasContext("good"));
        Assert.False(predictor.HasContext("lonely"));
        Assert.False(predictor.HasContext(null));
    }

    [Fact]
    public void PredictNext_KeepsTheCountOfEachSuggestion()
    {
        NextWordPredictor predictor = Build();

        WordEntry top = predictor.PredictNext("good").First();

        Assert.Equal("morning", top.Word);
        Assert.Equal(60, top.Frequency);
    }

    [Fact]
    public void AskingTwiceGivesTheSameFallbackList()
    {
        NextWordPredictor predictor = Build();

        // The fallback list is worked out once and kept, so this also checks
        // the cached copy is not accidentally emptied by the first call.
        List<string> first = predictor.PredictNextWords("lonely");
        List<string> second = predictor.PredictNextWords("lonely");

        Assert.Equal(first, second);
        Assert.NotEmpty(second);
    }

    [Fact]
    public void Refresh_MakesTheFallbackListPickUpNewCounts()
    {
        var words = new Trie();
        words.Insert("the", 100);

        var predictor = new NextWordPredictor(new BigramIndex(), words);
        Assert.Equal("the", predictor.PredictNextWords("nothing")[0]);

        words.Insert("zebra", 99999);
        predictor.Refresh();

        Assert.Equal("zebra", predictor.PredictNextWords("nothing")[0]);
    }
}
