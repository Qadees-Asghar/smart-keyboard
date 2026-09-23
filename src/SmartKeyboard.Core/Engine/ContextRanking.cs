using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Ranks suggestions using the word you typed before, not just how common a
/// word is on its own.
///
///     Score(w) = 0.7 x P(w | previous word) + 0.3 x P(w)
///
/// where
///
///     P(w | previous) = count(previous, w) / count(previous)
///     P(w)            = count(w) / total count of every word
///
/// Both parts are chances between 0 and 1, so they can be added together
/// fairly. The context part carries most of the weight because it is the more
/// useful signal. The small P(w) part stops a rare word winning just because
/// it happened to appear once after the previous word.
///
/// With no previous word there is nothing to condition on, so the score falls
/// back to P(w) alone.
///
/// This is the Strategy pattern in use: swapping this in for FrequencyRanking
/// changes the whole ranking without touching SuggestionEngine.
/// </summary>
public class ContextRanking : IRankingStrategy
{
    /// <summary>How much the previous word counts.</summary>
    public const double ContextWeight = 0.7;

    /// <summary>How much plain popularity counts.</summary>
    public const double PopularityWeight = 0.3;

    private readonly BigramIndex _bigrams;
    private readonly Trie _words;

    public ContextRanking(BigramIndex bigrams, Trie words)
    {
        _bigrams = bigrams ?? throw new ArgumentNullException(nameof(bigrams));
        _words = words ?? throw new ArgumentNullException(nameof(words));
    }

    // Works out how good a suggestion is. Bigger is better.
    // Time O(1) on average, because both lookups are hash map lookups.
    public double Score(WordEntry word, string? previousWord)
    {
        double popularity = Popularity(word);

        if (string.IsNullOrEmpty(previousWord) || !_bigrams.HasFollowers(previousWord))
        {
            // Nothing to go on, so judge the word on its own.
            return popularity;
        }

        double context = _bigrams.GetProbability(previousWord, word.Word);

        return (ContextWeight * context) + (PopularityWeight * popularity);
    }

    // P(w), the share of all typing this one word takes up.
    // Time O(1).
    private double Popularity(WordEntry word)
    {
        long total = _words.TotalFrequency;
        return total <= 0 ? 0.0 : (double)word.Frequency / total;
    }
}
