namespace SmartKeyboard.Core.Engine;

/// <summary>
/// The single place that decides whether the suggestion box belongs on screen.
///
/// It used to be decided in scattered bits of the system wide controller, and
/// the rule amounted to "one letter has been typed". That is why the box
/// appeared constantly: a single letter matches thousands of words, and
/// nothing asked whether the letter was even going into a text field.
///
/// The rules now, in one tested place with no Windows in them:
///
///   a completion needs two letters and somewhere to put them
///   a next word guess needs somewhere to put it and a session already running
///
/// The second rule is what lets both wishes hold at once. Suggesting the next
/// word happens when nothing is being typed, which would fall foul of "only
/// while typing" if it were judged on its own. Judged against a live session
/// it is fine: carrying on mid sentence counts as writing, pressing space
/// once on an idle screen does not.
/// </summary>
public static class SuggestionPolicy
{
    /// <summary>
    /// How many letters before completions are offered.
    ///
    /// One was the old behaviour and it was too eager. Two is enough to mean
    /// a word is genuinely being written, and it cuts out most of the boxes
    /// that appeared when nobody wanted one.
    /// </summary>
    public const int MinPrefixLength = 2;

    /// <summary>Why the box was not shown, for the diagnostics log.</summary>
    public enum Refusal
    {
        /// <summary>It was shown.</summary>
        None,

        /// <summary>The focused thing does not take typed text.</summary>
        NotATextTarget,

        /// <summary>Fewer letters typed than MinPrefixLength.</summary>
        TooFewLetters,

        /// <summary>A next word guess with no typing session running.</summary>
        NoSession,

        /// <summary>There were no words to show.</summary>
        NoWords,

        /// <summary>
        /// We did not watch this word from its first letter, so the letters
        /// on screen may not be the ones we think. Replacing would delete
        /// the wrong number of them.
        /// </summary>
        WordStartUnknown,
    }

    // Whether to offer words that finish what is being typed.
    // Time O(1).
    public static Refusal CheckCompletions(string? prefix, bool targetTakesText, bool wordStartKnown)
    {
        if (!targetTakesText)
        {
            return Refusal.NotATextTarget;
        }

        // Accepting a completion replaces what was typed, so it is only safe
        // when what was typed is really what is in front of the caret.
        if (!wordStartKnown)
        {
            return Refusal.WordStartUnknown;
        }

        return (prefix ?? string.Empty).Length < MinPrefixLength
            ? Refusal.TooFewLetters
            : Refusal.None;
    }

    // Whether to offer a guess at the next word, after a finished one.
    // Time O(1).
    public static Refusal CheckPredictions(bool targetTakesText, bool sessionLive)
    {
        if (!targetTakesText)
        {
            return Refusal.NotATextTarget;
        }

        return sessionLive ? Refusal.None : Refusal.NoSession;
    }

    /// <summary>True when completions should be offered.</summary>
    // Time O(1).
    public static bool ShouldShowCompletions(string? prefix, bool targetTakesText, bool wordStartKnown)
    {
        return CheckCompletions(prefix, targetTakesText, wordStartKnown) == Refusal.None;
    }

    /// <summary>True when a next word guess should be offered.</summary>
    // Time O(1).
    public static bool ShouldShowPredictions(bool targetTakesText, bool sessionLive)
    {
        return CheckPredictions(targetTakesText, sessionLive) == Refusal.None;
    }
}
