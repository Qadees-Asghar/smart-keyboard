using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Turns the prefix you are typing into a short list of suggested words.
/// It asks the Trie for every match, scores each one with the current
/// ranking strategy, and uses the Max Heap to pull out the best few.
/// </summary>
public class SuggestionEngine
{
    /// <summary>How many suggestions to show by default.</summary>
    public const int DefaultSuggestionCount = 5;

    /// <summary>
    /// How much rarer than the word you already typed a longer word may be
    /// and still be worth offering. A fifth as common is the limit.
    /// </summary>
    public const int CompletionRatio = 5;

    /// <summary>The mark people leave out of a contraction.</summary>
    public const char Apostrophe = '\'';

    private readonly Trie _trie;

    public SuggestionEngine(Trie trie, IRankingStrategy? ranking = null)
    {
        _trie = trie ?? throw new ArgumentNullException(nameof(trie));
        Ranking = ranking ?? new FrequencyRanking();
    }

    /// <summary>The scoring rule in use. Can be swapped at any time.</summary>
    public IRankingStrategy Ranking { get; set; }

    // Finds the best words that start with the prefix.
    // Time O(L + M + M log M) where L is the prefix length and M is the number
    // of matches: O(L) to walk the Trie, O(M) to collect and score the matches,
    // and O(k log M) to pop the top k out of the heap.
    public List<WordEntry> GetSuggestions(string? prefix, string? previousWord = null, int count = DefaultSuggestionCount)
    {
        if (string.IsNullOrWhiteSpace(prefix) || count <= 0)
        {
            return new List<WordEntry>();
        }

        // A contraction typed without its apostrophe comes first. "cant"
        // cannot reach "can't" through the Trie, because the mark sits in the
        // middle of the word, so the prefix walk goes off down "canterbury,
        // canton, cantonese" instead. Every letter of "can't" was typed, so
        // it belongs at the top, not missing entirely.
        List<WordEntry> contractions = ContractionsFor(prefix, count);

        List<WordEntry> matches = _trie.GetWordsWithPrefix(prefix);
        if (matches.Count == 0)
        {
            return contractions;
        }

        int floor = CompletionFloor(prefix);

        var heap = new MaxHeap<WordEntry>();
        foreach (WordEntry match in matches)
        {
            if (match.Frequency < floor)
            {
                continue;
            }

            heap.Push(match, Ranking.Score(match, previousWord));
        }

        if (contractions.Count == 0)
        {
            return heap.PopTop(count);
        }

        // Keep the best of the ordinary matches after them, without repeating
        // anything the contraction list already holds.
        var seen = contractions.Select(word => word.Word).ToHashSet(StringComparer.Ordinal);
        List<WordEntry> rest = heap.PopTop(count)
            .Where(word => !seen.Contains(word.Word))
            .ToList();

        contractions.AddRange(rest);
        return contractions.Take(count).ToList();
    }

    // Finds the words the user would have typed if they had not left the
    // apostrophe out. "cant" gives "can't", "couldn" gives "couldn't".
    //
    // The mark is only tried inside the word. One at the start or the end is
    // a quote or a possessive, not a contraction.
    // Time O(L) walks of the Trie, each O(L) plus whatever it finds.
    private List<WordEntry> ContractionsFor(string prefix, int count)
    {
        var found = new List<WordEntry>();

        if (prefix.Length < 2 || prefix.Contains(Apostrophe))
        {
            return found;
        }

        for (int i = 1; i < prefix.Length; i++)
        {
            string withMark = prefix[..i] + Apostrophe + prefix[i..];

            foreach (WordEntry match in _trie.GetWordsWithPrefix(withMark))
            {
                found.Add(match);
            }
        }

        return found
            .OrderByDescending(word => word.Frequency)
            .Take(count)
            .ToList();
    }

    // The smallest a longer word may be and still be offered.
    //
    // This is the difference between a useful list and a useless one. Once
    // what you have typed is already a word, almost every longer word that
    // starts with it is rarer, and most of them are surnames. Typing "and"
    // offered "andrew, anderson, andy, andreas", and typing "how" offered
    // "however, howard, howe, howl". Nobody typing "and" wanted "anderson".
    //
    // So once the prefix is itself a word, a longer word has to be at least a
    // fifth as common to be worth showing. "how" keeps "however", which is a
    // real thing to mean instead, and loses the surnames. "and" and "the"
    // lose everything, which is right: you had already typed what you meant,
    // and an empty list simply closes the popup.
    //
    // A prefix that is not yet a word, like "hel" or "th", is not filtered at
    // all, because there the whole point is to finish it for you.
    // Time O(L) to look the prefix up.
    private int CompletionFloor(string prefix)
    {
        int typed = _trie.GetFrequency(prefix);

        return typed <= 0 ? 0 : typed / CompletionRatio;
    }

    // Same as GetSuggestions but gives back only the words, no counts.
    // Handy for the UI. Time is the same as GetSuggestions.
    public List<string> GetSuggestionWords(string? prefix, string? previousWord = null, int count = DefaultSuggestionCount)
    {
        return GetSuggestions(prefix, previousWord, count).Select(w => w.Word).ToList();
    }

    // Picks the most common words in the whole dictionary. Used as a fallback
    // when there is nothing typed yet and no prediction is available.
    // Time O(N + k log N) where N is the number of words in the dictionary.
    public List<WordEntry> GetMostFrequentWords(int count = DefaultSuggestionCount)
    {
        if (count <= 0)
        {
            return new List<WordEntry>();
        }

        var heap = new MaxHeap<WordEntry>();
        foreach (WordEntry word in _trie.GetAllWords())
        {
            heap.Push(word, word.Frequency);
        }

        return heap.PopTop(count);
    }
}
