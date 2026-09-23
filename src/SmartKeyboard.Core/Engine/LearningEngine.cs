using SmartKeyboard.Core.Data;
using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Learns from what you actually type. This is counting, nothing more.
/// There is no AI and no machine learning anywhere in here.
///
/// When you finish a word that is already a real word, its count goes up.
/// When two real words are typed one after the other, that pair's count goes
/// up. Higher counts mean the word is suggested sooner next time.
///
/// It never learns a word it does not already know, so a typo you type fifty
/// times never becomes a word the spell checker accepts. To teach it a new
/// word you add it to your own dictionary on purpose.
///
/// The counts are saved to learned.txt. words.txt and bigrams.txt are never
/// written to, so the dictionary that shipped with the program stays as it was.
///
/// One change from a plain "add 1 each time". The shipped counts are measured
/// over an enormous amount of text, so a common pair sits in the hundreds.
/// Adding 1 per use would take hundreds of repetitions before your own habit
/// counted for anything, which is not learning anyone would ever notice.
///
/// So one use is worth Weight, which is worked out from the dictionary itself
/// rather than fixed. Swap in a bigger word list and the weight grows with it.
/// </summary>
public class LearningEngine
{
    /// <summary>The smallest a single use is ever worth.</summary>
    public const int MinimumWeight = 25;

    /// <summary>
    /// One use by you is worth about a thousandth of the commonest word in
    /// the language. That is roughly the size of a well used word pair, so a
    /// handful of repetitions is enough for your own habit to win.
    /// </summary>
    public const int WeightDivisor = 1000;

    /// <summary>How many changes to collect before writing the file.</summary>
    public const int SaveEvery = 10;

    private readonly IWordRepository _repository;
    private readonly Trie _words;
    private readonly BigramIndex _bigrams;
    private readonly UserDictionary? _users;

    private LearnedCounts _learned = new();
    private int _unsavedChanges;

    public LearningEngine(
        IWordRepository repository,
        Trie words,
        BigramIndex bigrams,
        UserDictionary? users = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _words = words ?? throw new ArgumentNullException(nameof(words));
        _bigrams = bigrams ?? throw new ArgumentNullException(nameof(bigrams));
        _users = users;
    }

    /// <summary>Turned on or off in Settings.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// What one use by the user is worth. Measured against the dictionary
    /// that is actually loaded, so it stays meaningful at any size.
    /// </summary>
    public int Weight => Math.Max(MinimumWeight, _words.MaxFrequency / WeightDivisor);

    /// <summary>How many words have been learned about.</summary>
    public int LearnedWordCount => _learned.Words.Count;

    /// <summary>How many word pairs have been learned about.</summary>
    public int LearnedPairCount => _learned.Pairs.Count;

    /// <summary>Raised after counts change, so lists can be rebuilt.</summary>
    public event EventHandler? Changed;

    // Reads learned.txt and adds the saved counts on top of the dictionary.
    // Call this once at startup, after the dictionary has been loaded.
    // Time O(N) over the saved counts.
    public void Load()
    {
        _learned = _repository.LoadLearned();

        foreach (KeyValuePair<string, int> word in _learned.Words)
        {
            _words.Insert(word.Key, word.Value);
        }

        foreach (KeyValuePair<string, int> pair in _learned.Pairs)
        {
            string[] parts = pair.Key.Split(' ', 2);
            if (parts.Length == 2)
            {
                _bigrams.Add(parts[0], parts[1], pair.Value);
            }
        }
    }

    // Records that you finished a word, and which word came before it.
    // Both are ignored unless they are words the program already knows.
    // Time O(L) for the Trie, O(1) on average for the pair.
    public void RecordWord(string? word, string? previousWord = null)
    {
        if (!Enabled)
        {
            return;
        }

        string current = Normalize(word);
        if (!IsLearnable(current))
        {
            return;
        }

        AddToWord(current);

        string previous = Normalize(previousWord);
        if (IsLearnable(previous))
        {
            AddToPair(previous, current);
        }

        _unsavedChanges++;
        if (_unsavedChanges >= SaveEvery)
        {
            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Records a pair on its own, without touching the single word counts.
    // Time O(1) on average.
    public void RecordPair(string? previousWord, string? nextWord)
    {
        if (!Enabled)
        {
            return;
        }

        string previous = Normalize(previousWord);
        string next = Normalize(nextWord);

        if (!IsLearnable(previous) || !IsLearnable(next))
        {
            return;
        }

        AddToPair(previous, next);
        _unsavedChanges++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True when the word is real, so learning about it is safe.</summary>
    // Time O(L).
    public bool IsLearnable(string? word)
    {
        string clean = Normalize(word);

        if (clean.Length == 0 || clean.Any(char.IsDigit))
        {
            return false;
        }

        // Only words the program already knows, so typos are never learned.
        return _words.Contains(clean) || (_users?.Contains(clean) ?? false);
    }

    /// <summary>How much has been learned about one word, on top of the books.</summary>
    // Time O(1) on average.
    public int GetLearnedCount(string? word)
    {
        return _learned.Words.TryGetValue(Normalize(word), out int count) ? count : 0;
    }

    /// <summary>How much has been learned about one pair.</summary>
    // Time O(1) on average.
    public int GetLearnedPairCount(string? previousWord, string? nextWord)
    {
        string key = $"{Normalize(previousWord)} {Normalize(nextWord)}";
        return _learned.Pairs.TryGetValue(key, out int count) ? count : 0;
    }

    /// <summary>Writes learned.txt now, instead of waiting for the next batch.</summary>
    // Time O(N) over the learned counts.
    public void Save()
    {
        _repository.SaveLearned(_learned);
        _unsavedChanges = 0;
    }

    // Forgets everything learned, and takes the extra counts back out of the
    // dictionary so the program behaves as it did on the first run.
    // Time O(N) over the learned counts.
    public void Reset()
    {
        foreach (KeyValuePair<string, int> word in _learned.Words)
        {
            int current = _words.GetFrequency(word.Key);
            _words.SetFrequency(word.Key, Math.Max(0, current - word.Value));
        }

        // A BigramIndex only adds up, so the pair counts are put back by
        // adding the same amount as a negative.
        foreach (KeyValuePair<string, int> pair in _learned.Pairs)
        {
            string[] parts = pair.Key.Split(' ', 2);
            if (parts.Length == 2)
            {
                _bigrams.Add(parts[0], parts[1], -pair.Value);
            }
        }

        _learned = new LearnedCounts();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Raises the count of one word, in memory and in the saved list.
    // Time O(L).
    private void AddToWord(string word)
    {
        int weight = Weight;

        _learned.Words[word] = GetLearnedCount(word) + weight;
        _words.Insert(word, weight);
    }

    // Raises the count of one pair, in memory and in the saved list.
    // Time O(1) on average.
    private void AddToPair(string previous, string next)
    {
        string key = $"{previous} {next}";
        int weight = Weight;

        _learned.Pairs[key] = (_learned.Pairs.TryGetValue(key, out int existing) ? existing : 0) + weight;
        _bigrams.Add(previous, next, weight);
    }

    // Lower cases and trims. Time O(L).
    private static string Normalize(string? word)
    {
        return word is null ? string.Empty : word.Trim().ToLowerInvariant();
    }
}
