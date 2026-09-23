using SmartKeyboard.Core.Data;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests;

/// <summary>
/// Learning against the real dictionary, which is the case that matters.
///
/// The shipped counts come from whole books, so a user's habit has to be able
/// to beat a Victorian novel in a handful of repetitions, not sixty.
/// </summary>
[Collection("real dictionary")]
public class LearningOnRealDataTests
{
    private readonly RealDictionary _dictionary;

    public LearningOnRealDataTests(RealDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    // A private copy of the real data, so one test cannot affect another.
    private static (LearningEngine Learning, Trie Words, BigramIndex Bigrams) BuildCopy(
        RealDictionary source, TempDataFolder folder)
    {
        var words = new Trie();
        foreach (var entry in source.Words.GetAllWords())
        {
            words.Insert(entry.Word, entry.Frequency);
        }

        var bigrams = new BigramIndex();
        foreach (string first in new[] { "good", "how", "thank", "i" })
        {
            foreach (var follower in source.Bigrams.GetFollowers(first))
            {
                bigrams.Add(first, follower.Word, follower.Frequency);
            }
        }

        var repository = new FileWordRepository(folder.Path);
        return (new LearningEngine(repository, words, bigrams), words, bigrams);
    }

    [Fact]
    public void TypingGoodMorningAFewTimesMakesItTheTopPrediction()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, BigramIndex bigrams) = BuildCopy(_dictionary, folder);

        var predictor = new NextWordPredictor(bigrams, words);

        // The shipped data has "good for", "good to" and "good idea" well ahead
        // of "good morning", so it does not appear at all to begin with.
        Assert.DoesNotContain("morning", predictor.PredictNextWords("good"));

        // Type it three times, the way someone actually would.
        for (int i = 0; i < 3; i++)
        {
            learning.RecordWord("morning", previousWord: "good");
        }

        Assert.Equal("morning", predictor.PredictNextWords("good").First());
    }

    [Fact]
    public void OneUseIsNotEnoughToRewriteEverything()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, BigramIndex bigrams) = BuildCopy(_dictionary, folder);

        var predictor = new NextWordPredictor(bigrams, words);

        learning.RecordWord("morning", previousWord: "good");

        // One use is not enough to knock the strongest pair off the list.
        // "good for" is the commonest thing to follow "good" in the data.
        List<string> next = predictor.PredictNextWords("good");
        Assert.Contains("for", next);
    }

    [Fact]
    public void TyposAreNeverLearnedNoMatterHowOftenTheyAreTyped()
    {
        using var folder = new TempDataFolder();
        (LearningEngine learning, Trie words, _) = BuildCopy(_dictionary, folder);

        foreach (string typo in new[] { "hwo", "aer", "mornign", "teh" })
        {
            for (int i = 0; i < 20; i++)
            {
                learning.RecordWord(typo, previousWord: "good");
            }

            Assert.False(words.Contains(typo), $"\"{typo}\" was learned as a real word");
        }

        Assert.Equal(0, learning.LearnedWordCount);
    }

    [Fact]
    public void ContextRankingPutsTheRightWordFirstAfterAKnownWord()
    {
        var ranking = new ContextRanking(_dictionary.Bigrams, _dictionary.Words);
        var engine = new SuggestionEngine(_dictionary.Words, ranking);

        // "of the" is the commonest pair in English, so after "of" the prefix
        // "th" must put "the" first.
        Assert.Equal("the", engine.GetSuggestionWords("th", "of").First());
    }

    [Fact]
    public void ContextRankingStillWorksWithNoPreviousWord()
    {
        var ranking = new ContextRanking(_dictionary.Bigrams, _dictionary.Words);
        var engine = new SuggestionEngine(_dictionary.Words, ranking);

        List<string> words = engine.GetSuggestionWords("th", null);

        Assert.NotEmpty(words);
        Assert.All(words, w => Assert.StartsWith("th", w));
    }
}
