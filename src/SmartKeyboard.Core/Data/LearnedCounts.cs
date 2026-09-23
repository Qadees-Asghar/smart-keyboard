namespace SmartKeyboard.Core.Data;

/// <summary>
/// The extra counts picked up from watching the user type.
/// These are kept separate from words.txt and bigrams.txt so the original
/// dictionary files are never changed.
/// </summary>
public sealed class LearnedCounts
{
    /// <summary>Extra count for single words.</summary>
    public Dictionary<string, int> Words { get; } = new();

    /// <summary>Extra count for word pairs, keyed by "first second".</summary>
    public Dictionary<string, int> Pairs { get; } = new();

    /// <summary>True when nothing has been learned yet.</summary>
    public bool IsEmpty => Words.Count == 0 && Pairs.Count == 0;
}
