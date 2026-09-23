using SmartKeyboard.Core.Data;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class UserDictionaryTests
{
    // Builds a user dictionary backed by a throwaway folder.
    private static (UserDictionary Dictionary, Trie Trie, BKTree Tree, FileWordRepository Repository)
        Build(TempDataFolder folder)
    {
        var trie = new Trie();
        trie.Insert("the", 1000);
        trie.Insert("keyboard", 100);

        var tree = new BKTree();
        tree.Add("the");
        tree.Add("keyboard");

        var repository = new FileWordRepository(folder.Path);
        var dictionary = new UserDictionary(repository, trie, tree);

        return (dictionary, trie, tree, repository);
    }

    [Fact]
    public void Add_PutsTheWordInTheList()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, _) = Build(folder);

        Assert.True(dictionary.Add("qadees"));

        Assert.True(dictionary.Contains("qadees"));
        Assert.Equal(1, dictionary.Count);
    }

    [Fact]
    public void Add_MakesTheWordSearchableStraightAway()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, Trie trie, BKTree tree, _) = Build(folder);

        dictionary.Add("qadees");

        Assert.True(trie.Contains("qadees"));
        Assert.True(tree.Contains("qadees"));
        Assert.Equal(UserDictionary.DefaultFrequency, trie.GetFrequency("qadees"));
    }

    [Fact]
    public void Add_TheSameWordTwiceIsIgnoredTheSecondTime()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, _) = Build(folder);

        Assert.True(dictionary.Add("qadees"));
        Assert.False(dictionary.Add("qadees"));
        Assert.False(dictionary.Add("QADEES"));

        Assert.Equal(1, dictionary.Count);
    }

    [Fact]
    public void Add_IgnoresCaseAndSpaces()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, _) = Build(folder);

        dictionary.Add("  Qadees  ");

        Assert.True(dictionary.Contains("qadees"));
        Assert.Equal(new[] { "qadees" }, dictionary.GetAll());
    }

    [Fact]
    public void Add_RefusesWordsWithDigitsOrPunctuation()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, _) = Build(folder);

        Assert.False(dictionary.Add("abc123"));
        Assert.False(dictionary.Add("hello world"));
        Assert.False(dictionary.Add("what?"));
        Assert.False(dictionary.Add(string.Empty));
        Assert.False(dictionary.Add(null));

        Assert.Equal(0, dictionary.Count);
    }

    [Fact]
    public void Add_AllowsAWordWithAnApostrophe()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, _) = Build(folder);

        Assert.True(dictionary.Add("o'brien"));
    }

    [Fact]
    public void Add_DoesNotChangeTheCountOfAWordTheDictionaryAlreadyHas()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, Trie trie, _, _) = Build(folder);

        dictionary.Add("the");

        Assert.Equal(1000, trie.GetFrequency("the"));
    }

    [Fact]
    public void Remove_TakesTheWordOutOfTheListAndTheTrie()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, Trie trie, _, _) = Build(folder);

        dictionary.Add("qadees");
        Assert.True(dictionary.Remove("qadees"));

        Assert.False(dictionary.Contains("qadees"));
        Assert.False(trie.Contains("qadees"));
        Assert.Equal(0, dictionary.Count);
    }

    [Fact]
    public void Remove_ReturnsFalseForAWordThatWasNeverAdded()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, _) = Build(folder);

        Assert.False(dictionary.Remove("nothing"));
    }

    [Fact]
    public void Remove_MeansTheWordIsNoLongerOfferedAsAFix()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, Trie trie, BKTree tree, _) = Build(folder);

        dictionary.Add("qadees");
        var matcher = new FuzzyMatcher(tree, trie);
        Assert.Contains("qadees", matcher.FindCorrections("qadeer").Select(f => f.Word));

        dictionary.Remove("qadees");

        // The word is still inside the BK tree, because a BK tree cannot drop a
        // node. The matcher must check the Trie and leave it out anyway.
        Assert.DoesNotContain("qadees", matcher.FindCorrections("qadeer").Select(f => f.Word));
    }

    [Fact]
    public void GetAll_ReturnsTheWordsInAlphabeticalOrder()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, _) = Build(folder);

        dictionary.Add("zebra");
        dictionary.Add("apple");
        dictionary.Add("mango");

        Assert.Equal(new[] { "apple", "mango", "zebra" }, dictionary.GetAll());
    }

    [Fact]
    public void Add_SavesToFileSoTheWordSurvivesARestart()
    {
        using var folder = new TempDataFolder();
        (UserDictionary first, _, _, _) = Build(folder);

        first.Add("qadees");
        first.Add("seecs");

        // A brand new dictionary, as if the program had just started again.
        (UserDictionary second, Trie trie, _, _) = Build(folder);
        second.Load();

        Assert.Equal(2, second.Count);
        Assert.True(second.Contains("qadees"));
        Assert.True(trie.Contains("seecs"));
    }

    [Fact]
    public void Load_OnAFreshInstallFindsNothingAndDoesNotCrash()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, _) = Build(folder);

        dictionary.Load();

        Assert.Equal(0, dictionary.Count);
    }

    [Fact]
    public void Load_ThrowsAwayWhatWasInMemoryBefore()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, _, _, FileWordRepository repository) = Build(folder);

        dictionary.Add("qadees");
        repository.SaveUserWords(new[] { "different" });
        dictionary.Load();

        Assert.False(dictionary.Contains("qadees"));
        Assert.True(dictionary.Contains("different"));
    }

    [Fact]
    public void Clear_EmptiesTheListAndTheTrie()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, Trie trie, _, _) = Build(folder);

        dictionary.Add("qadees");
        dictionary.Add("seecs");
        dictionary.Clear();

        Assert.Equal(0, dictionary.Count);
        Assert.False(trie.Contains("qadees"));

        // Still gone after a reload, so the file was cleared too.
        dictionary.Load();
        Assert.Equal(0, dictionary.Count);
    }

    [Fact]
    public void Clear_LeavesTheShippedDictionaryAlone()
    {
        using var folder = new TempDataFolder();
        (UserDictionary dictionary, Trie trie, _, _) = Build(folder);

        dictionary.Add("qadees");
        dictionary.Clear();

        Assert.True(trie.Contains("the"));
        Assert.True(trie.Contains("keyboard"));
    }

    [Theory]
    [InlineData("hello", true)]
    [InlineData("o'brien", true)]
    [InlineData("abc123", false)]
    [InlineData("two words", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowed_AcceptsOnlyRealSingleWords(string? word, bool expected)
    {
        Assert.Equal(expected, UserDictionary.IsAllowed(word));
    }

    [Fact]
    public void WorkingWithoutABKTreeStillWorks()
    {
        using var folder = new TempDataFolder();
        var trie = new Trie();
        var dictionary = new UserDictionary(new FileWordRepository(folder.Path), trie);

        Assert.True(dictionary.Add("qadees"));
        Assert.True(trie.Contains("qadees"));
    }
}
