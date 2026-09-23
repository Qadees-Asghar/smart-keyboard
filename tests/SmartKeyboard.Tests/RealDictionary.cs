using SmartKeyboard.Core.Data;
using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Tests;

/// <summary>
/// Loads the real words.txt and bigrams.txt once, and shares them with every
/// test that needs them. Loading takes a moment, so doing it once keeps the
/// test run fast.
/// </summary>
public sealed class RealDictionary
{
    public RealDictionary()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Data");
        var repository = new FileWordRepository(folder);

        var clock = System.Diagnostics.Stopwatch.StartNew();

        Words = new Trie();
        foreach (var entry in repository.LoadWords())
        {
            Words.Insert(entry.Word, entry.Frequency);
        }

        Bigrams = new BigramIndex();
        foreach (var entry in repository.LoadBigrams())
        {
            Bigrams.Add(entry.First, entry.Second, entry.Count);
        }

        clock.Stop();
        LoadTimeMs = clock.ElapsedMilliseconds;

        // The BK tree is only needed for typo fixing, so it is built after
        // the parts the app needs to start.
        var treeClock = System.Diagnostics.Stopwatch.StartNew();

        Tree = new BKTree();
        foreach (var entry in Words.GetAllWords())
        {
            Tree.Add(entry.Word);
        }

        treeClock.Stop();
        TreeBuildMs = treeClock.ElapsedMilliseconds;
    }

    public Trie Words { get; }

    public BigramIndex Bigrams { get; }

    public BKTree Tree { get; }

    public long LoadTimeMs { get; }

    public long TreeBuildMs { get; }
}

/// <summary>Tells xUnit to build RealDictionary once for the whole collection.</summary>
[CollectionDefinition("real dictionary")]
public class RealDictionaryCollection : ICollectionFixture<RealDictionary>
{
}
