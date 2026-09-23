namespace SmartKeyboard.Core.Engine;

/// <summary>
/// What autocorrect decided to do about one word.
/// The editor needs the original word back so Ctrl+Z can put it there again.
/// </summary>
public sealed class AutocorrectResult
{
    private AutocorrectResult(bool changed, string original, string corrected, string reason)
    {
        Changed = changed;
        Original = original;
        Corrected = corrected;
        Reason = reason;
    }

    /// <summary>True when the word was replaced.</summary>
    public bool Changed { get; }

    /// <summary>The word the user actually typed.</summary>
    public string Original { get; }

    /// <summary>The word to put in its place. Same as Original when nothing changed.</summary>
    public string Corrected { get; }

    /// <summary>Why the decision went that way. Useful for the status bar and for tests.</summary>
    public string Reason { get; }

    /// <summary>Nothing was changed.</summary>
    public static AutocorrectResult Keep(string word, string reason) =>
        new(false, word, word, reason);

    /// <summary>The word was replaced.</summary>
    public static AutocorrectResult Replace(string original, string corrected) =>
        new(true, original, corrected, "corrected");

    public override string ToString() =>
        Changed ? $"{Original} -> {Corrected}" : $"{Original} kept ({Reason})";
}
