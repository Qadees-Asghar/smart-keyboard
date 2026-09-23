using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Fixes a typo automatically the moment you finish a word, which is when you
/// press space or punctuation.
///
/// Autocorrect that guesses wrong is worse than no autocorrect at all, because
/// it silently changes what you wrote. So it only acts when it is nearly
/// certain, and the rules below are all about refusing to guess.
///
/// A word with ONE mistake is replaced when the best candidate is at least
/// twice as common as the runner up.
///
/// A word with TWO mistakes is messier, and there are usually several words
/// that could have been meant, so the bar is raised: the word must be at
/// least 6 letters long, and the winner must be four times as common as the
/// runner up. Long words carry enough letters to make the guess safe, while
/// short ones do not.
///
/// The margin rule is the important one either way. If two real words are
/// equally close to what you typed, there is no way to tell which you meant,
/// so it leaves it alone and lets the red underline do the talking instead.
///
/// It never touches:
///   words it already knows          they are spelled fine
///   words you added yourself        your own names and terms
///   anything with a digit in it     like a4 or covid19
///   a Capitalised word mid sentence they are usually names
/// </summary>
public class AutocorrectEngine
{
    /// <summary>Margin needed when the word is one mistake away.</summary>
    public const double MarginForOneMistake = 2.0;

    /// <summary>Margin needed when the word is two mistakes away. Much stricter.</summary>
    public const double MarginForTwoMistakes = 4.0;

    /// <summary>The furthest a word can be and still be fixed without asking.</summary>
    public const int MaxDistance = 2;

    /// <summary>A word must be at least this long before two mistakes are fixed.</summary>
    public const int MinLengthForTwoMistakes = 6;

    private readonly FuzzyMatcher _fuzzy;
    private readonly Trie _words;
    private readonly UserDictionary? _users;

    public AutocorrectEngine(FuzzyMatcher fuzzy, Trie words, UserDictionary? users = null)
    {
        _fuzzy = fuzzy ?? throw new ArgumentNullException(nameof(fuzzy));
        _words = words ?? throw new ArgumentNullException(nameof(words));
        _users = users;
    }

    /// <summary>Turned on or off in Settings. On by default in Editor Mode.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether messier words, two mistakes away, may be fixed as well.
    /// On by default. Turning it off leaves only the safest corrections.
    /// </summary>
    public bool FixMessyWords { get; set; } = true;

    // Decides what to do with a finished word.
    // isSentenceStart tells it whether the word is the first of a sentence,
    // because that is the only place a capital letter means nothing special.
    // Time is the same as FuzzyMatcher.FindCorrections.
    public AutocorrectResult Check(string? word, bool isSentenceStart = false)
    {
        string typed = word ?? string.Empty;

        if (!Enabled)
        {
            return AutocorrectResult.Keep(typed, "autocorrect is off");
        }

        if (typed.Trim().Length == 0)
        {
            return AutocorrectResult.Keep(typed, "not a word");
        }

        // Anything with a digit is a code, a model number or a version.
        if (typed.Any(char.IsDigit))
        {
            return AutocorrectResult.Keep(typed, "has a digit in it");
        }

        string lower = typed.ToLowerInvariant();

        // The user's own words are checked first. Adding a word also puts it
        // into the main Trie, so the general check below would otherwise
        // answer first with a vaguer reason.
        if (_users is not null && _users.Contains(lower))
        {
            return AutocorrectResult.Keep(typed, "you added this word");
        }

        if (_words.Contains(lower))
        {
            return AutocorrectResult.Keep(typed, "already a real word");
        }

        // A capital in the middle of a sentence nearly always means a name.
        if (!isSentenceStart && char.IsUpper(typed[0]))
        {
            return AutocorrectResult.Keep(typed, "looks like a name");
        }

        List<FuzzyMatch> candidates = _fuzzy.FindCorrections(lower);

        if (candidates.Count == 0)
        {
            return AutocorrectResult.Keep(typed, "no close word found");
        }

        FuzzyMatch best = candidates[0];

        if (best.Distance > MaxDistance)
        {
            return AutocorrectResult.Keep(typed, "closest word is too far away");
        }

        if (best.Distance == 2)
        {
            if (!FixMessyWords)
            {
                return AutocorrectResult.Keep(typed, "too messy to fix on its own");
            }

            if (lower.Length < MinLengthForTwoMistakes)
            {
                return AutocorrectResult.Keep(typed, "too short to guess at two mistakes");
            }
        }

        double margin = best.Distance >= 2 ? MarginForTwoMistakes : MarginForOneMistake;

        // With a runner up in the running, the winner has to be clearly ahead.
        if (candidates.Count > 1 && !IsClearWinner(best, candidates[1], margin))
        {
            return AutocorrectResult.Keep(typed, "two words are too close to call");
        }

        return AutocorrectResult.Replace(typed, WordScanner.MatchCapitalization(typed, best.Word));
    }

    // True when the best candidate is far enough ahead of the runner up to be
    // trusted. A runner up that is further away does not count as competition.
    // Time O(1).
    private static bool IsClearWinner(FuzzyMatch best, FuzzyMatch runnerUp, double margin)
    {
        if (runnerUp.Distance > best.Distance)
        {
            return true;
        }

        // A more likely kind of slip wins outright, whatever the counts say.
        //
        // Typing "helo" puts "help", "held", "hero" and "hello" all one edit
        // away, and "help" is the commonest by a wide margin, so on frequency
        // alone the answer would be "help". But the two l's in "hello" are
        // the same key pressed twice and only one registered, which is a far
        // more likely slip than hitting p instead of o.
        //
        // Typing "hav" puts "have", "has" and "had" one edit away with no
        // count able to separate them, so this used to give up. Only "have"
        // keeps the v that was actually pressed.
        //
        // The counts only decide between two candidates of the same kind.
        if (best.SlipRank < runnerUp.SlipRank)
        {
            return true;
        }

        // A word nobody uses is no competition either.
        if (runnerUp.Frequency <= 0)
        {
            return true;
        }

        return best.Frequency >= margin * runnerUp.Frequency;
    }
}
