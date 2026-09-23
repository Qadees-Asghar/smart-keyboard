namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Strategy pattern. Decides how good a suggestion is.
/// A bigger score means the word should be shown higher in the list.
/// Swapping the strategy changes the ranking without touching the engine.
/// </summary>
public interface IRankingStrategy
{
    /// <param name="word">The candidate word and its dictionary frequency.</param>
    /// <param name="previousWord">The word typed before this one, or null.</param>
    double Score(WordEntry word, string? previousWord);
}
