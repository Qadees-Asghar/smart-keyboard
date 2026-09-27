using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Fixes a word that is spelled correctly but is the wrong word, like "form"
/// for "from" or "then" for "than".
///
/// Ordinary autocorrect cannot see these at all, because every one of them is
/// in the dictionary. The only thing that gives them away is the words around
/// them: "email form the office" is almost never written, "email from the
/// office" constantly is.
///
/// So the check needs BOTH neighbours, and runs on a word only once the word
/// after it has been typed. Each alternative c is scored
///
///     score(c) = P(c | word before) x P(word after | c)
///
/// from the word pair data, and the typed word is scored the same way. The
/// word is swapped only when an alternative scores at least RequiredRatio
/// times as high as what was typed, and the pair data has actually seen it
/// next to one of those neighbours.
///
/// Each side's say is capped at MaxSideRatio. The pair data is patchy, and a
/// pair it happens never to have seen would otherwise count as infinitely
/// unlikely. "made off with" was being changed to "made of with" because the
/// data has no "made off", although "off with" beats "of with" nine to one on
/// the other side. With the cap, one missing pair cannot outvote the other
/// side.
///
/// What the user's own typing has taught SmartKeyboard is left out of the
/// counts here. Learning adds a pair the moment it is typed, weighted as a
/// couple of hundred sightings, so typing "email form" once made "email form"
/// look normal before this check had even run, and the mixup was never
/// fixed. Learning still shapes suggestions; it just cannot vote here.
///
/// It only ever considers a small list of words people really mix up. Letting
/// it choose from the whole dictionary would turn every rare phrase into a
/// "mistake". Words with an apostrophe are left out, because the pair data
/// has no apostrophes in it and could never speak up for them.
///
/// Off by default. A wrong swap here changes a real word into another real
/// word, which is harder to notice than a typo.
/// </summary>
public class RealWordChecker
{
    /// <summary>
    /// How many times more likely the alternative must be. High on purpose:
    /// "a different form of" is correct, and must stay that way.
    /// </summary>
    public const double RequiredRatio = 8.0;

    /// <summary>The most one side, before or after, can count for on its own.</summary>
    public const double MaxSideRatio = 50.0;

    /// <summary>How much of each chance comes from the pair data rather than plain frequency.</summary>
    public const double PairWeight = 0.9;

    /// <summary>The groups of words people type in place of each other.</summary>
    public static readonly string[][] ConfusionSets =
    {
        new[] { "from", "form" },
        new[] { "than", "then" },
        new[] { "of", "off" },
        new[] { "to", "too" },
        new[] { "lose", "loose" },
        new[] { "quite", "quiet" },
        new[] { "whether", "weather" },
        new[] { "buy", "by" },
        new[] { "there", "their" },
        new[] { "accept", "except" },
        new[] { "affect", "effect" },
        new[] { "advice", "advise" },
        new[] { "breath", "breathe" },
        new[] { "past", "passed" },
        new[] { "here", "hear" },
        new[] { "know", "now" },
        new[] { "were", "where" },
    };

    private static readonly Dictionary<string, string[]> Alternatives = BuildAlternatives();

    private readonly BigramIndex _bigrams;
    private readonly Trie _words;
    private readonly Func<string, string, int>? _learnedPairCount;

    // learnedPairCount says how much of a pair's count came from the user's
    // own typing, so it can be taken back out. LearningEngine.GetLearnedPairCount
    // is the one to pass.
    public RealWordChecker(
        BigramIndex bigrams, Trie words, Func<string, string, int>? learnedPairCount = null)
    {
        _bigrams = bigrams ?? throw new ArgumentNullException(nameof(bigrams));
        _words = words ?? throw new ArgumentNullException(nameof(words));
        _learnedPairCount = learnedPairCount;
    }

    /// <summary>Turned on in Settings. Off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>True when the word is one this checker knows people mix up.</summary>
    public static bool IsConfusable(string? word) =>
        word is not null && Alternatives.ContainsKey(word.ToLowerInvariant());

    // Decides whether "word", sitting between "before" and "after", should be
    // a different word. Either neighbour may be null, at the start of a
    // sentence for instance, but not both.
    // Time O(k) for the k alternatives, each a few hash lookups.
    public AutocorrectResult Check(string? before, string? word, string? after)
    {
        string typed = word ?? string.Empty;

        if (!Enabled)
        {
            return AutocorrectResult.Keep(typed, "real word fixing is off");
        }

        string lower = typed.ToLowerInvariant();
        if (!Alternatives.TryGetValue(lower, out string[]? alternatives))
        {
            return AutocorrectResult.Keep(typed, "not a word people mix up");
        }

        string? left = Clean(before);
        string? right = Clean(after);

        if (left is null && right is null)
        {
            return AutocorrectResult.Keep(typed, "nothing around it to go on");
        }

        string? best = null;
        double bestAdvantage = 0;

        foreach (string alternative in alternatives)
        {
            if (!HasEvidence(left, alternative, right))
            {
                continue;
            }

            double advantage = Advantage(left, lower, alternative, right);
            if (advantage > bestAdvantage)
            {
                best = alternative;
                bestAdvantage = advantage;
            }
        }

        if (best is null)
        {
            return AutocorrectResult.Keep(typed, "no alternative fits better");
        }

        if (bestAdvantage < RequiredRatio)
        {
            return AutocorrectResult.Keep(typed, "what was typed fits well enough");
        }

        return AutocorrectResult.Replace(typed, WordScanner.MatchCapitalization(typed, best));
    }

    // How many times better the alternative fits between the neighbours than
    // the word that was typed, each side capped at MaxSideRatio.
    // Time O(1) on average.
    private double Advantage(string? left, string typed, string alternative, string? right)
    {
        double advantage = SideRatio(Chance(left, alternative), Chance(left, typed));

        if (right is not null)
        {
            advantage *= SideRatio(Chance(alternative, right), Chance(typed, right));
        }

        return advantage;
    }

    // One side's ratio, kept between 1 / MaxSideRatio and MaxSideRatio.
    // Time O(1).
    private static double SideRatio(double alternative, double typed)
    {
        if (typed <= 0)
        {
            return alternative <= 0 ? 1.0 : MaxSideRatio;
        }

        return Math.Clamp(alternative / typed, 1.0 / MaxSideRatio, MaxSideRatio);
    }

    // P(second | first), mixed with a little of how common "second" is on its
    // own, so one pair the data never saw does not zero out the whole score.
    // With no first word it is just how common the word is.
    // Time O(1) on average.
    private double Chance(string? first, string second)
    {
        double plain = Popularity(second);

        if (first is null || !_bigrams.HasFollowers(first))
        {
            return plain;
        }

        double pair = (double)PairCount(first, second) / _bigrams.GetTotalAfter(first);

        return (PairWeight * pair) + ((1 - PairWeight) * plain);
    }

    // How often the pair appears in the shipped data, with the user's own
    // learned typing taken back out. Time O(1) on average.
    private int PairCount(string first, string second)
    {
        int count = _bigrams.GetCount(first, second);

        if (_learnedPairCount is not null)
        {
            count -= _learnedPairCount(first, second);
        }

        return Math.Max(0, count);
    }

    // True when the pair data has seen this word next to at least one of its
    // neighbours. A word never seen in either place is not a real contender,
    // however common it is. Time O(1) on average.
    private bool HasEvidence(string? left, string word, string? right)
    {
        return (left is not null && PairCount(left, word) > 0)
            || (right is not null && PairCount(word, right) > 0);
    }

    // The share of all typing this word takes up. Time O(L).
    private double Popularity(string word)
    {
        long total = _words.TotalFrequency;
        return total <= 0 ? 0.0 : (double)_words.GetFrequency(word) / total;
    }

    private static string? Clean(string? word)
    {
        return string.IsNullOrWhiteSpace(word) ? null : word.Trim().ToLowerInvariant();
    }

    // Turns the groups into a lookup from each word to the others in its
    // group. Time O(total words in all groups).
    private static Dictionary<string, string[]> BuildAlternatives()
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (string[] set in ConfusionSets)
        {
            foreach (string word in set)
            {
                map[word] = set.Where(other => other != word).ToArray();
            }
        }

        return map;
    }
}
