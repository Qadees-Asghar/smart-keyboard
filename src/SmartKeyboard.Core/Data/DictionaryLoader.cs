using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Data;

/// <summary>
/// Singleton pattern. Reads the dictionary files once and keeps the Trie and
/// the bigram index in memory for the whole run of the program.
///
/// Both Editor Mode and System Wide Mode share this one copy, so the data is
/// never loaded twice and memory stays low.
/// </summary>
public sealed class DictionaryLoader
{
    private static readonly Lazy<DictionaryLoader> Lazy = new(() => new DictionaryLoader());

    private readonly object _lock = new();

    private DictionaryLoader()
    {
        Words = new Trie();
        Bigrams = new BigramIndex();
    }

    /// <summary>The one and only instance.</summary>
    public static DictionaryLoader Instance => Lazy.Value;

    /// <summary>Every known word, ready for prefix search.</summary>
    public Trie Words { get; private set; }

    /// <summary>Which word usually follows which.</summary>
    public BigramIndex Bigrams { get; private set; }

    /// <summary>True once the files have been read.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>How long the last load took, in milliseconds.</summary>
    public long LoadTimeMs { get; private set; }

    // Reads the files and builds the data structures.
    // Calling it again reloads from scratch, which the tests rely on.
    // Time O(W * L + B) where W is the number of words, L the average word
    // length, and B the number of word pairs.
    public void Load(IWordRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        lock (_lock)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();

            var words = new Trie();
            foreach (WordEntry entry in repository.LoadWords())
            {
                words.Insert(entry.Word, entry.Frequency);
            }

            var bigrams = new BigramIndex();
            foreach (BigramEntry entry in repository.LoadBigrams())
            {
                bigrams.Add(entry.First, entry.Second, entry.Count);
            }

            Words = words;
            Bigrams = bigrams;
            IsLoaded = true;

            started.Stop();
            LoadTimeMs = started.ElapsedMilliseconds;
        }
    }

    // Throws the data away. Used by tests so one test cannot affect the next.
    public void Reset()
    {
        lock (_lock)
        {
            Words = new Trie();
            Bigrams = new BigramIndex();
            IsLoaded = false;
            LoadTimeMs = 0;
        }
    }
}
