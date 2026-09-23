using System.Diagnostics;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests;

/// <summary>
/// Next word prediction against the real 90,000 word pairs.
/// </summary>
[Collection("real dictionary")]
public class PredictionOnRealDataTests
{
    private readonly RealDictionary _dictionary;

    public PredictionOnRealDataTests(RealDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    private NextWordPredictor Predictor() => new(_dictionary.Bigrams, _dictionary.Words);

    [Theory]
    [InlineData("of")]
    [InlineData("in")]
    [InlineData("to")]
    [InlineData("good")]
    [InlineData("thank")]
    [InlineData("i")]
    public void CommonWordsAllHaveRealPredictions(string word)
    {
        NextWordPredictor predictor = Predictor();

        List<string> next = predictor.PredictNextWords(word);

        Assert.Equal(5, next.Count);
        Assert.False(predictor.LastAnswerWasFallback, $"\"{word}\" fell back instead of using real pair data");
    }

    [Fact]
    public void ThePredictionsForOfLookLikeRealEnglish()
    {
        List<string> next = Predictor().PredictNextWords("of");

        // "of the" is the commonest word pair in English, so it must be first.
        Assert.Equal("the", next[0]);
    }

    [Fact]
    public void ProbabilitiesAddUpToNoMoreThanOne()
    {
        NextWordPredictor predictor = Predictor();

        foreach (string word in new[] { "of", "in", "good", "the" })
        {
            double total = 0;
            foreach (var follower in _dictionary.Bigrams.GetFollowers(word))
            {
                total += predictor.GetProbability(word, follower.Word);
            }

            Assert.True(total <= 1.0001, $"the chances after \"{word}\" added up to {total}");
            Assert.True(total > 0.99, $"the chances after \"{word}\" only added up to {total}");
        }
    }

    [Fact]
    public void AWordWithNoPairDataStillGetsAnAnswer()
    {
        NextWordPredictor predictor = Predictor();

        List<string> next = predictor.PredictNextWords("zzzznotaword");

        Assert.Equal(5, next.Count);
        Assert.True(predictor.LastAnswerWasFallback);
        Assert.Equal("the", next[0]);
    }

    [Fact]
    public void PredictingIsFastEnoughToRunOnEveryFinishedWord()
    {
        NextWordPredictor predictor = Predictor();
        string[] words = { "of", "in", "the", "good", "and", "to", "a" };

        foreach (string word in words)
        {
            predictor.PredictNextWords(word);
        }

        foreach (string word in words)
        {
            var clock = Stopwatch.StartNew();
            predictor.PredictNextWords(word);
            clock.Stop();

            Assert.True(
                clock.Elapsed.TotalMilliseconds < 100,
                $"predicting after \"{word}\" took {clock.Elapsed.TotalMilliseconds:0.0} ms");
        }
    }
}
