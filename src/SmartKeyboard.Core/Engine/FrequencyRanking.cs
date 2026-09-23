namespace SmartKeyboard.Core.Engine;

/// <summary>
/// The simplest ranking. The more common a word is, the higher it is shown.
/// The previous word is ignored. ContextRanking replaces this later.
/// </summary>
public class FrequencyRanking : IRankingStrategy
{
    // Time O(1).
    public double Score(WordEntry word, string? previousWord)
    {
        return word.Frequency;
    }
}
