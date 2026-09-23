using SmartKeyboard.Core;
using SmartKeyboard.Core.Data;

namespace SmartKeyboard.Tests.Data;

public class FileWordRepositoryTests
{
    [Fact]
    public void LoadWords_ReadsWordAndCountFromEachLine()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "the 100", "cat 50");
        folder.Write("bigrams.txt", "the cat 10");

        var repository = new FileWordRepository(folder.Path);
        List<WordEntry> words = repository.LoadWords().ToList();

        Assert.Equal(2, words.Count);
        Assert.Equal("the", words[0].Word);
        Assert.Equal(100, words[0].Frequency);
    }

    [Fact]
    public void LoadWords_LowerCasesEveryWord()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "The 100", "CAT 50");
        folder.Write("bigrams.txt");

        var repository = new FileWordRepository(folder.Path);
        List<string> words = repository.LoadWords().Select(w => w.Word).ToList();

        Assert.Equal(new[] { "the", "cat" }, words);
    }

    [Fact]
    public void LoadWords_SkipsBlankAndBrokenLines()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "the 100", string.Empty, "   ", "# a comment", "broken notanumber", "cat 50");
        folder.Write("bigrams.txt");

        var repository = new FileWordRepository(folder.Path);
        List<WordEntry> words = repository.LoadWords().ToList();

        // "broken notanumber" keeps the word and falls back to a count of 1.
        Assert.Equal(new[] { "the", "broken", "cat" }, words.Select(w => w.Word));
        Assert.Equal(1, words[1].Frequency);
    }

    [Fact]
    public void LoadWords_MissingFileGivesAClearError()
    {
        using var folder = new TempDataFolder();

        var repository = new FileWordRepository(folder.Path);
        var error = Assert.Throws<FileNotFoundException>(() => repository.LoadWords());

        Assert.Contains("words.txt", error.Message);
    }

    [Fact]
    public void LoadBigrams_ReadsTwoWordsAndACount()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "the 1");
        folder.Write("bigrams.txt", "the cat 10", "the dog 5");

        var repository = new FileWordRepository(folder.Path);
        List<BigramEntry> pairs = repository.LoadBigrams().ToList();

        Assert.Equal(2, pairs.Count);
        Assert.Equal("the", pairs[0].First);
        Assert.Equal("cat", pairs[0].Second);
        Assert.Equal(10, pairs[0].Count);
    }

    [Fact]
    public void LoadBigrams_SkipsLinesThatDoNotHaveTwoWords()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "the 1");
        folder.Write("bigrams.txt", "the cat 10", "lonely", string.Empty, "the dog 5");

        var repository = new FileWordRepository(folder.Path);

        Assert.Equal(2, repository.LoadBigrams().Count());
    }

    [Fact]
    public void LoadBigrams_MissingFileGivesAClearError()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "the 1");

        var repository = new FileWordRepository(folder.Path);
        var error = Assert.Throws<FileNotFoundException>(() => repository.LoadBigrams());

        Assert.Contains("bigrams.txt", error.Message);
    }

    [Fact]
    public void LoadUserWords_ReturnsNothingWhenTheFileIsNotThere()
    {
        using var folder = new TempDataFolder();

        var repository = new FileWordRepository(folder.Path);

        Assert.Empty(repository.LoadUserWords());
    }

    [Fact]
    public void SaveUserWords_ThenLoadUserWords_GivesTheSameWordsBack()
    {
        using var folder = new TempDataFolder();
        var repository = new FileWordRepository(folder.Path);

        repository.SaveUserWords(new[] { "Qadees", "seecs", "   ", "nustian" });

        Assert.Equal(new[] { "qadees", "seecs", "nustian" }, repository.LoadUserWords());
    }

    [Fact]
    public void SaveLearned_ThenLoadLearned_GivesTheSameCountsBack()
    {
        using var folder = new TempDataFolder();
        var repository = new FileWordRepository(folder.Path);

        var counts = new LearnedCounts();
        counts.Words["hello"] = 7;
        counts.Pairs["hello there"] = 3;
        repository.SaveLearned(counts);

        LearnedCounts loaded = repository.LoadLearned();

        Assert.Equal(7, loaded.Words["hello"]);
        Assert.Equal(3, loaded.Pairs["hello there"]);
    }

    [Fact]
    public void LoadLearned_ReturnsEmptyWhenTheFileIsNotThere()
    {
        using var folder = new TempDataFolder();

        Assert.True(new FileWordRepository(folder.Path).LoadLearned().IsEmpty);
    }
}
