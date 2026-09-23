using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Finds real words that are close to a typo.
///
/// The rule for how far to look comes from the project spec:
///   words of 4 letters or less  look 1 edit away
///   longer words                look 2 edits away
///
/// Short words are kept strict on purpose. With a limit of 2 almost every
/// three letter word is close to every other one, and the fixes become useless.
///
/// Results are sorted by distance first, then by how common the word is.
///
/// Four extra rules sit on top of plain edit distance. None of them changes
/// how far the search looks. They change which of the words it found is
/// picked, because plain edit distance treats every kind of slip as equally
/// likely, and real fingers do not.
///
/// First, an apostrophe left out. "cant" is one edit from "can't", "canto",
/// "can" and "want", and "canto" is used six times in the whole word list, so
/// it used to win simply because "cant" is the start of it. Every letter of
/// "can't" was typed correctly and in the right order though, and the
/// apostrophe is the key people skip most, often deliberately.
///
/// Second, a missed or repeated double letter is put ahead of other words the
/// same distance away. "helo" is one edit from "help", "held", "hero" and
/// "hello", and "help" is the commonest, so frequency alone answers "help".
/// The two l's in "hello" are the same key pressed twice though, and only one
/// of them registering is a much more likely slip than hitting the wrong key.
///
/// Third, a word you did not finish beats a word with a letter changed.
/// "hav" is one edit from "have", "has", "had" and "hat". Frequency alone
/// cannot separate "have" from "has", so it used to give up and leave the
/// typo alone. But "have" keeps every letter that was actually pressed and
/// only lacks an ending, while "has" claims the v was a typing error. Not
/// finishing a word is much more likely. The same rule stops "wor" becoming
/// "for", which is what used to happen, because "for" is ten times commoner
/// than "work" and one letter away.
///
/// Fourth, swapping two neighbouring letters, like "teh" for "the", is counted
/// as ONE mistake, not two. Plain
/// Levenshtein calls that 2 edits, which would put the commonest typo in
/// English out of reach for a short word. Swaps are checked straight against
/// the Trie, so the BK tree keeps using real Levenshtein and its branch
/// skipping stays correct.
/// </summary>
public class FuzzyMatcher
{
    /// <summary>Words this long or shorter only look 1 edit away.</summary>
    public const int ShortWordLength = 4;

    /// <summary>How far to look for a short word.</summary>
    public const int ShortWordMaxDistance = 1;

    /// <summary>How far to look for a longer word.</summary>
    public const int LongWordMaxDistance = 2;

    /// <summary>How many fixes to offer by default.</summary>
    public const int DefaultSuggestionCount = 5;

    /// <summary>A word shorter than this is never fixed by doubling a letter.</summary>
    public const int MinLengthForDoubleLetterFix = 4;

    /// <summary>The mark that is missing from a typed contraction.</summary>
    public const char Apostrophe = '\'';

    /// <summary>
    /// A word shorter than this is never treated as an unfinished one. Two
    /// letters are the start of far too many words to guess from.
    /// </summary>
    public const int MinLengthForCompletion = 3;

    private readonly BKTree _tree;
    private readonly Trie _words;

    public FuzzyMatcher(BKTree tree, Trie words)
    {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _words = words ?? throw new ArgumentNullException(nameof(words));
    }

    // Picks how far to look based on the length of the word.
    // Time O(1).
    public static int GetMaxDistance(string word)
    {
        return word.Length <= ShortWordLength ? ShortWordMaxDistance : LongWordMaxDistance;
    }

    // Finds the best fixes for a misspelled word.
    // Returns an empty list when the word is already spelled correctly.
    // Time is the BK tree search plus O(m log m) to sort the m matches found.
    public List<FuzzyMatch> FindCorrections(string? word, int count = DefaultSuggestionCount)
    {
        string clean = (word ?? string.Empty).Trim().ToLowerInvariant();
        if (clean.Length == 0 || count <= 0)
        {
            return new List<FuzzyMatch>();
        }

        // A word we already know needs no fixing.
        if (_words.Contains(clean))
        {
            return new List<FuzzyMatch>();
        }

        List<FuzzyMatch> found = _tree.Search(clean, GetMaxDistance(clean));

        // Keep the closest version of each word, so a word found by both the
        // tree and the swap check is not listed twice.
        var best = new Dictionary<string, int>();
        foreach (FuzzyMatch match in found)
        {
            // The Trie is the real dictionary. A word can still sit in the BK
            // tree after the user removed it, because a BK tree cannot drop a
            // node without rebuilding, so skip anything the Trie no longer has.
            if (!_words.Contains(match.Word))
            {
                continue;
            }

            if (!best.TryGetValue(match.Word, out int existing) || match.Distance < existing)
            {
                best[match.Word] = match.Distance;
            }
        }

        // Apostrophes are checked straight against the Trie, the same way
        // swaps are, so a contraction is always found whatever the tree did.
        var apostrophes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string fixedUp in GetApostropheFixes(clean))
        {
            apostrophes.Add(fixedUp);

            if (!best.TryGetValue(fixedUp, out int existing) || existing > 1)
            {
                best[fixedUp] = 1;
            }
        }

        var swaps = new HashSet<string>(StringComparer.Ordinal);
        foreach (string swapped in GetLetterSwaps(clean))
        {
            if (!_words.Contains(swapped))
            {
                continue;
            }

            swaps.Add(swapped);

            if (!best.TryGetValue(swapped, out int existing) || existing > 1)
            {
                best[swapped] = 1;
            }
        }

        HashSet<string> doubles = GetDoubleLetterFixes(clean);

        return best
            .Select(pair => new FuzzyMatch(
                pair.Key,
                pair.Value,
                _words.GetFrequency(pair.Key),
                apostrophes.Contains(pair.Key),
                doubles.Contains(pair.Key),
                swaps.Contains(pair.Key),
                IsCompletionOf(clean, pair.Key)))
            .OrderBy(match => match.Distance)
            .ThenBy(match => match.SlipRank)
            .ThenByDescending(match => match.Frequency)
            .ThenBy(match => match.Word, StringComparer.Ordinal)
            .Take(count)
            .ToList();
    }

    // Builds every real word made by putting an apostrophe back into this one.
    // "cant" gives "can't", "youre" gives "you're".
    //
    // These are one edit away, so the BK tree usually finds them anyway, but
    // they are looked up here as well for the same reason swaps are: it
    // guarantees a contraction is never missed, and it marks them so the
    // ranking knows what kind of slip they are.
    //
    // The mark is only ever put inside the word. An apostrophe at the very
    // start or the very end is a quote or a possessive, not a contraction.
    // Time O(L) candidates, each O(L) to build and O(L) to look up.
    public IEnumerable<string> GetApostropheFixes(string word)
    {
        if (word.Contains(Apostrophe))
        {
            // It already has one. Adding a second is not a slip anyone makes.
            yield break;
        }

        for (int i = 1; i < word.Length; i++)
        {
            string withMark = word[..i] + Apostrophe + word[i..];
            if (_words.Contains(withMark))
            {
                yield return withMark;
            }
        }
    }

    // Builds every word made by swapping two neighbouring letters.
    // "teh" gives "eth" and "the". Time O(L) words, each O(L) to build.
    public static IEnumerable<string> GetLetterSwaps(string word)
    {
        for (int i = 0; i + 1 < word.Length; i++)
        {
            if (word[i] == word[i + 1])
            {
                continue;
            }

            char[] letters = word.ToCharArray();
            (letters[i], letters[i + 1]) = (letters[i + 1], letters[i]);
            yield return new string(letters);
        }
    }

    // Finds the real words that differ from this one by a single doubled
    // letter, in either direction: "helo" gives "hello", "untill" gives
    // "until". Only words the dictionary knows are returned.
    //
    // Both of those are already one edit away, so the BK tree finds them
    // anyway. The point of this is ranking. "helo" is one edit from "help",
    // "held", "hero" and "hello", and "help" is by far the commonest, so on
    // frequency alone the fix is "help". Doubling is a far more likely slip
    // than changing a letter, because the two keypresses are the same key and
    // only one of them registered, so "hello" is the better answer.
    //
    // Very short words are left out. Doubling a letter in a three letter
    // fragment guesses too much from too little.
    // Time O(L) candidates, each O(L) to build and O(L) to look up.
    public HashSet<string> GetDoubleLetterFixes(string word)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        if (word.Length < MinLengthForDoubleLetterFix)
        {
            return found;
        }

        // A letter that should have been typed twice.
        for (int i = 0; i < word.Length; i++)
        {
            string doubled = word[..i] + word[i] + word[i..];
            if (_words.Contains(doubled))
            {
                found.Add(doubled);
            }
        }

        // A letter that was typed twice when once was right.
        for (int i = 0; i + 1 < word.Length; i++)
        {
            if (word[i] != word[i + 1])
            {
                continue;
            }

            string single = word[..i] + word[(i + 1)..];
            if (_words.Contains(single))
            {
                found.Add(single);
            }
        }

        return found;
    }

    /// <summary>True when the typo is the beginning of this longer word.</summary>
    // Time O(L).
    public static bool IsCompletionOf(string typed, string candidate)
    {
        return typed.Length >= MinLengthForCompletion
            && candidate.Length > typed.Length
            && candidate.StartsWith(typed, StringComparison.Ordinal);
    }

    /// <summary>True when the word is not in the dictionary.</summary>
    // Time O(L).
    public bool IsMisspelled(string? word)
    {
        string clean = (word ?? string.Empty).Trim().ToLowerInvariant();
        return clean.Length > 0 && !_words.Contains(clean);
    }

    // Adds a word so it can be offered as a fix from now on.
    // Time is the same as BKTree.Add.
    public void Add(string word)
    {
        _tree.Add(word);
    }
}
