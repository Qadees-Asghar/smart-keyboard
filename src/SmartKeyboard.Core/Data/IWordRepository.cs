namespace SmartKeyboard.Core.Data;

/// <summary>
/// Repository pattern. Hides where the word data actually lives.
/// Today it is text files. A test can supply a fake instead, and nothing
/// else in the program has to change.
/// </summary>
public interface IWordRepository
{
    /// <summary>Reads every "word count" line.</summary>
    IEnumerable<WordEntry> LoadWords();

    /// <summary>Reads every "word1 word2 count" line.</summary>
    IEnumerable<BigramEntry> LoadBigrams();

    /// <summary>Reads the words the user added by hand. Empty when there are none.</summary>
    IEnumerable<string> LoadUserWords();

    /// <summary>Saves the user's own words, replacing whatever was saved before.</summary>
    void SaveUserWords(IEnumerable<string> words);

    /// <summary>Reads the extra counts learned from the user's typing.</summary>
    LearnedCounts LoadLearned();

    /// <summary>Saves the learned counts, replacing whatever was saved before.</summary>
    void SaveLearned(LearnedCounts counts);
}
