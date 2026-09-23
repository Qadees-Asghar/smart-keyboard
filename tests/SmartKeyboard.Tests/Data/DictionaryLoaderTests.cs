using SmartKeyboard.Core.Data;

namespace SmartKeyboard.Tests.Data;

public class DictionaryLoaderTests
{
    [Fact]
    public void Instance_IsAlwaysTheSameObject()
    {
        Assert.Same(DictionaryLoader.Instance, DictionaryLoader.Instance);
    }

    [Fact]
    public void Load_FillsTheTrieAndTheBigramIndex()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "good 100", "morning 40", "night 20");
        folder.Write("bigrams.txt", "good morning 30", "good night 10");

        DictionaryLoader loader = DictionaryLoader.Instance;
        loader.Load(new FileWordRepository(folder.Path));

        try
        {
            Assert.True(loader.IsLoaded);
            Assert.Equal(3, loader.Words.WordCount);
            Assert.True(loader.Words.Contains("morning"));
            Assert.Equal(100, loader.Words.GetFrequency("good"));
            Assert.Equal(30, loader.Bigrams.GetCount("good", "morning"));
            Assert.Equal(40, loader.Bigrams.GetTotalAfter("good"));
        }
        finally
        {
            loader.Reset();
        }
    }

    [Fact]
    public void Load_Twice_StartsFreshInsteadOfDoublingTheCounts()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "good 100");
        folder.Write("bigrams.txt", "good morning 30");

        DictionaryLoader loader = DictionaryLoader.Instance;
        var repository = new FileWordRepository(folder.Path);

        try
        {
            loader.Load(repository);
            loader.Load(repository);

            Assert.Equal(1, loader.Words.WordCount);
            Assert.Equal(100, loader.Words.GetFrequency("good"));
            Assert.Equal(30, loader.Bigrams.GetCount("good", "morning"));
        }
        finally
        {
            loader.Reset();
        }
    }

    [Fact]
    public void Reset_EmptiesEverything()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "good 100");
        folder.Write("bigrams.txt", "good morning 30");

        DictionaryLoader loader = DictionaryLoader.Instance;
        loader.Load(new FileWordRepository(folder.Path));
        loader.Reset();

        Assert.False(loader.IsLoaded);
        Assert.Equal(0, loader.Words.WordCount);
        Assert.Equal(0, loader.Bigrams.PairCount);
    }

    [Fact]
    public void Load_WithNoRepositoryThrows()
    {
        Assert.Throws<ArgumentNullException>(() => DictionaryLoader.Instance.Load(null!));
    }
}
