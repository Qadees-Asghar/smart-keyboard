using SmartKeyboard.Core;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class ContextRankingTests
{
    private static (ContextRanking Ranking, Trie Words, BigramIndex Bigrams) Build()
    {
        var words = new Trie();
        words.Insert("morning", 100);
        words.Insert("more", 900);
        words.Insert("money", 500);
        words.Insert("good", 1000);

        var bigrams = new BigramIndex();

        // After "good", "morning" is the usual word even though "more" is a
        // much more common word overall.
        bigrams.Add("good", "morning", 90);
        bigrams.Add("good", "more", 10);

        return (new ContextRanking(bigrams, words), words, bigrams);
    }

    [Fact]
    public void ContextBeatsPlainPopularity()
    {
        (ContextRanking ranking, _, _) = Build();

        var morning = new WordEntry("morning", 100);
        var more = new WordEntry("more", 900);

        // On its own "more" wins. After "good", "morning" must win.
        Assert.True(ranking.Score(more, null) > ranking.Score(morning, null));
        Assert.True(ranking.Score(morning, "good") > ranking.Score(more, "good"));
    }

    [Fact]
    public void WithNoPreviousWordTheScoreIsJustPopularity()
    {
        (ContextRanking ranking, Trie words, _) = Build();

        var more = new WordEntry("more", 900);
        double expected = 900.0 / words.TotalFrequency;

        Assert.Equal(expected, ranking.Score(more, null), 10);
        Assert.Equal(expected, ranking.Score(more, string.Empty), 10);
    }

    [Fact]
    public void AnUnseenPreviousWordFallsBackToPopularity()
    {
        (ContextRanking ranking, Trie words, _) = Build();

        var more = new WordEntry("more", 900);
        double expected = 900.0 / words.TotalFrequency;

        Assert.Equal(expected, ranking.Score(more, "zzzznotaword"), 10);
    }

    [Fact]
    public void TheScoreUsesTheWeightsFromTheSpec()
    {
        (ContextRanking ranking, Trie words, BigramIndex bigrams) = Build();

        var morning = new WordEntry("morning", 100);

        double context = bigrams.GetProbability("good", "morning");
        double popularity = 100.0 / words.TotalFrequency;
        double expected = (0.7 * context) + (0.3 * popularity);

        Assert.Equal(expected, ranking.Score(morning, "good"), 10);
        Assert.Equal(0.7, ContextRanking.ContextWeight);
        Assert.Equal(0.3, ContextRanking.PopularityWeight);
    }

    [Fact]
    public void AWordNeverSeenAfterThePreviousWordStillScoresSomething()
    {
        (ContextRanking ranking, _, _) = Build();

        // "money" never follows "good", so its context part is zero, but its
        // popularity still counts for something.
        double score = ranking.Score(new WordEntry("money", 500), "good");

        Assert.True(score > 0);
    }

    [Fact]
    public void ScoresAreNeverNegative()
    {
        (ContextRanking ranking, _, _) = Build();

        foreach (string? previous in new[] { null, "good", "money", "zzz" })
        {
            Assert.True(ranking.Score(new WordEntry("morning", 100), previous) >= 0);
        }
    }

    [Fact]
    public void AnEmptyDictionaryDoesNotDivideByZero()
    {
        var ranking = new ContextRanking(new BigramIndex(), new Trie());

        Assert.Equal(0.0, ranking.Score(new WordEntry("anything", 5), null));
    }

    [Fact]
    public void ItPlugsIntoTheSuggestionEngineAndChangesTheOrder()
    {
        (ContextRanking ranking, Trie words, _) = Build();

        var engine = new SuggestionEngine(words);

        // Plain frequency puts "more" first for the prefix "mo".
        Assert.Equal("more", engine.GetSuggestionWords("mo").First());

        // The context strategy puts "morning" first, after "good".
        engine.Ranking = ranking;
        Assert.Equal("morning", engine.GetSuggestionWords("mo", "good").First());
    }
}
