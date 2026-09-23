using SmartKeyboard.Core.Data;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class LearningEngineTests
{
    private static (LearningEngine Learning, Trie Words, BigramIndex Bigrams, UserDictionary Users)
        Build(TempDataFolder folder)
    {
        var words = new Trie();
        words.Insert("good", 1000);
        words.Insert("morning", 6);
        words.Insert("bye", 65);
        words.Insert("night", 54);

        var bigrams = new BigramIndex();
        bigrams.Add("good", "bye", 65);
        bigrams.Add("good", "night", 54);
        bigrams.Add("good", "morning", 6);

        var repository = new FileWordRepository(folder.Path);
        var users = new UserDictionary(repository, words);
        var learning = new LearningEngine(repository, words, bigrams, users);

        return (learning, words, bigrams, users);
    }

    [Fact]
    public void RecordWord_RaisesTheCountOfARealWord()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, _, _) = Build(folder);

        learning.RecordWord("morning");

        Assert.Equal(6 + learning.Weight, words.GetFrequency("morning"));
        Assert.Equal(learning.Weight, learning.GetLearnedCount("morning"));
    }

    [Fact]
    public void RecordWord_AlsoLearnsThePairWithTheWordBefore()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, _, BigramIndex bigrams, _) = Build(folder);

        learning.RecordWord("morning", previousWord: "good");

        Assert.Equal(6 + learning.Weight, bigrams.GetCount("good", "morning"));
    }

    [Fact]
    public void TypingAPairAFewTimesIsEnoughToBeatWhatTheBooksSay()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, BigramIndex bigrams, _) = Build(folder);

        var predictor = new NextWordPredictor(bigrams, words);

        // The books say "good bye" far more often than "good morning".
        Assert.Equal("bye", predictor.PredictNextWords("good").First());

        // Type "good morning" three times, like a person would in the morning.
        for (int i = 0; i < 3; i++)
        {
            learning.RecordWord("morning", previousWord: "good");
        }

        Assert.Equal("morning", predictor.PredictNextWords("good").First());
    }

    [Fact]
    public void ATypoIsNeverLearned()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, _, _) = Build(folder);

        for (int i = 0; i < 50; i++)
        {
            learning.RecordWord("mornign", previousWord: "good");
        }

        Assert.False(words.Contains("mornign"));
        Assert.Equal(0, learning.GetLearnedCount("mornign"));
        Assert.Equal(0, learning.LearnedWordCount);
    }

    [Fact]
    public void AWordYouAddedYourselfIsLearnedFrom()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, _, _, UserDictionary users) = Build(folder);

        users.Add("qadees");
        learning.RecordWord("qadees");

        Assert.Equal(learning.Weight, learning.GetLearnedCount("qadees"));
    }

    [Fact]
    public void AnythingWithADigitIsNeverLearned()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, _, _, _) = Build(folder);

        learning.RecordWord("covid19");

        Assert.Equal(0, learning.LearnedWordCount);
    }

    [Fact]
    public void NoPairIsLearnedWhenTheWordBeforeIsNotARealWord()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, _, BigramIndex bigrams, _) = Build(folder);

        learning.RecordWord("morning", previousWord: "zzznotaword");

        // The pair was never there and must not be created.
        Assert.Equal(0, bigrams.GetCount("zzznotaword", "morning"));
        Assert.Equal(0, learning.LearnedPairCount);

        // The word itself is still learned, only the pair is skipped.
        Assert.Equal(learning.Weight, learning.GetLearnedCount("morning"));
    }

    [Fact]
    public void TurningItOffStopsEverything()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, _, _) = Build(folder);

        learning.Enabled = false;
        learning.RecordWord("morning", previousWord: "good");

        Assert.Equal(6, words.GetFrequency("morning"));
        Assert.Equal(0, learning.LearnedWordCount);
    }

    [Fact]
    public void WhatIsLearnedSurvivesARestart()
    {
        using var folder = new TempDataFolder();
        (LearningEngine first, _, _, _) = Build(folder);

        first.RecordWord("morning", previousWord: "good");
        first.RecordWord("morning", previousWord: "good");
        first.Save();

        // A fresh start, reading the saved file.
        (LearningEngine second, Trie words, BigramIndex bigrams, _) = Build(folder);
        second.Load();

        int expected = 2 * second.Weight;

        Assert.Equal(expected, second.GetLearnedCount("morning"));
        Assert.Equal(6 + expected, words.GetFrequency("morning"));
        Assert.Equal(6 + expected, bigrams.GetCount("good", "morning"));
    }

    [Fact]
    public void Load_OnAFreshInstallFindsNothingAndDoesNotCrash()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, _, _) = Build(folder);

        learning.Load();

        Assert.Equal(0, learning.LearnedWordCount);
        Assert.Equal(6, words.GetFrequency("morning"));
    }

    [Fact]
    public void Reset_PutsEverythingBackTheWayItStarted()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, BigramIndex bigrams, _) = Build(folder);

        for (int i = 0; i < 5; i++)
        {
            learning.RecordWord("morning", previousWord: "good");
        }

        learning.Reset();

        Assert.Equal(6, words.GetFrequency("morning"));
        Assert.Equal(6, bigrams.GetCount("good", "morning"));
        Assert.Equal(0, learning.LearnedWordCount);
        Assert.Equal(0, learning.LearnedPairCount);
    }

    [Fact]
    public void Reset_AlsoClearsTheSavedFile()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, _, _, _) = Build(folder);

        learning.RecordWord("morning", previousWord: "good");
        learning.Reset();

        (LearningEngine fresh, _, _, _) = Build(folder);
        fresh.Load();

        Assert.Equal(0, fresh.LearnedWordCount);
    }

    [Fact]
    public void LearningNeverTouchesTheShippedDictionaryFiles()
    {
        using var folder = new TempDataFolder();
        folder.Write("words.txt", "good 1000", "morning 6");
        folder.Write("bigrams.txt", "good morning 6");

        (LearningEngine learning, _, _, _) = Build(folder);
        for (int i = 0; i < 5; i++)
        {
            learning.RecordWord("morning", previousWord: "good");
        }

        learning.Save();

        // The files that shipped with the program must be byte for byte the same.
        Assert.Equal(new[] { "good 1000", "morning 6" }, folder.Read("words.txt"));
        Assert.Equal(new[] { "good morning 6" }, folder.Read("bigrams.txt"));
        Assert.True(folder.Exists("learned.txt"));
    }

    [Fact]
    public void RecordPair_LearnsAPairWithoutChangingTheWordCounts()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, BigramIndex bigrams, _) = Build(folder);

        learning.RecordPair("good", "morning");

        Assert.Equal(6 + learning.Weight, bigrams.GetCount("good", "morning"));
        Assert.Equal(6, words.GetFrequency("morning"));
    }

    [Fact]
    public void Changed_IsRaisedWhenSomethingIsLearned()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, _, _, _) = Build(folder);

        int raised = 0;
        learning.Changed += (_, _) => raised++;

        learning.RecordWord("morning", previousWord: "good");
        learning.RecordWord("zzznotaword");

        Assert.Equal(1, raised);
    }
}
