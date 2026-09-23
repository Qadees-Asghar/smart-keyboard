using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Guesses which word you are about to type, before you type any of it.
///
/// It works on word pairs counted from real sentences. If "good" was followed
/// by "morning" 60 times out of the 100 times "good" appeared, then
///
///     P(morning | good) = 60 / 100 = 0.6
///
/// The words with the highest chance are shown, most likely first. There is no
/// AI here, only counting and dividing.
///
/// When the previous word has never been seen with anything after it, the most
/// common words in the whole dictionary are offered instead, because that is
/// still a better guess than nothing.
///
/// A full stop, question mark or exclamation mark clears the context. What
/// came before the end of a sentence says nothing about what comes next, and
/// WordScanner.GetPreviousWord already returns null in that case.
/// </summary>
public class NextWordPredictor
{
    /// <summary>How many next words to offer by default.</summary>
    public const int DefaultCount = 5;

    private readonly BigramIndex _bigrams;
    private readonly Trie _words;

    // The fallback list is the same every time, so it is worked out once and
    // kept. Without this, every finished word would scan the whole dictionary.
    private List<WordEntry>? _commonWords;

    public NextWordPredictor(BigramIndex bigrams, Trie words)
    {
        _bigrams = bigrams ?? throw new ArgumentNullException(nameof(bigrams));
        _words = words ?? throw new ArgumentNullException(nameof(words));
    }

    /// <summary>True when the last answer came from the fallback list.</summary>
    public bool LastAnswerWasFallback { get; private set; }

    // Picks the most likely words to come after the given word.
    // Time O(F + k log F) where F is how many different words have followed it.
    // The heap gives the top k without sorting the whole list.
    public List<WordEntry> PredictNext(string? previousWord, int count = DefaultCount)
    {
        if (count <= 0)
        {
            return new List<WordEntry>();
        }

        List<WordEntry> followers = _bigrams.GetFollowers(previousWord);

        if (followers.Count == 0)
        {
            LastAnswerWasFallback = true;
            return GetCommonWords(count);
        }

        LastAnswerWasFallback = false;

        var heap = new MaxHeap<WordEntry>();
        foreach (WordEntry follower in followers)
        {
            heap.Push(follower, follower.Frequency);
        }

        return heap.PopTop(count);
    }

    // The same thing, but only the words. Handy for the UI.
    // Time is the same as PredictNext.
    public List<string> PredictNextWords(string? previousWord, int count = DefaultCount)
    {
        return PredictNext(previousWord, count).Select(w => w.Word).ToList();
    }

    // The chance that "next" follows "previous", between 0 and 1.
    // This is count(prev, next) divided by count(prev).
    // Time O(1) on average.
    public double GetProbability(string? previousWord, string? nextWord)
    {
        return _bigrams.GetProbability(previousWord, nextWord);
    }

    /// <summary>True when we have ever seen a word follow this one.</summary>
    // Time O(1) on average.
    public bool HasContext(string? previousWord)
    {
        return _bigrams.HasFollowers(previousWord);
    }

    /// <summary>Throws away the cached fallback list, after learning changed it.</summary>
    // Time O(1).
    public void Refresh()
    {
        _commonWords = null;
    }

    // The most common words in the whole dictionary, worked out once.
    // Time O(N + k log N) the first time, O(k) after that.
    private List<WordEntry> GetCommonWords(int count)
    {
        if (_commonWords is null)
        {
            var heap = new MaxHeap<WordEntry>();
            foreach (WordEntry word in _words.GetAllWords())
            {
                heap.Push(word, word.Frequency);
            }

            // Keep a few spare, so asking for more than the default still works
            // without scanning the dictionary again.
            _commonWords = heap.PopTop(Math.Max(count, DefaultCount * 4));
        }

        return _commonWords.Take(count).ToList();
    }
}
