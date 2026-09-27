namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Decides whether the text just before the caret in another app proves that
/// the word we followed is a whole word, and not the tail of a longer one.
///
/// After a click, an arrow key or a change of window we lose track of what is
/// in front of the caret, so the first word typed next could be the end of a
/// word that was already there. Clicking after "wor" and typing "ld" gives us
/// "ld", and correcting that would wreck "world". So that first word used to
/// be skipped: no suggestions and no correction.
///
/// When the app will tell us the few characters before the caret, that
/// question can be answered instead of guessed. The word is safe when the
/// text ends with exactly the letters we followed, and the character in front
/// of them is not part of a word, or there is nothing in front at all.
///
/// Anything unexpected is a no. Getting this wrong mangles text, while saying
/// no only costs one suggestion.
/// </summary>
public static class CaretTextCheck
{
    // True when the text before the caret ends with the expected text, and
    // what comes before that is a word boundary.
    //
    // requested is how many characters were asked for. Getting back fewer
    // than that means the start of the text was reached, which is a boundary
    // too. Time O(L) over the expected text.
    public static bool ConfirmsWord(string? textBeforeCaret, string? expected, int requested)
    {
        if (textBeforeCaret is null || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        if (!textBeforeCaret.EndsWith(expected, StringComparison.Ordinal))
        {
            return false;
        }

        if (textBeforeCaret.Length == expected.Length)
        {
            // Nothing in front of the word. That is only the start of the
            // text if more was asked for than came back.
            return requested > expected.Length;
        }

        char before = textBeforeCaret[textBeforeCaret.Length - expected.Length - 1];

        // A digit counts as part of the word here. "abc4" then "helo" is not
        // something to be guessing about.
        return !WordScanner.IsWordChar(before) && !char.IsLetterOrDigit(before);
    }

    // The same, for a word still being typed. The keyboard hook sees a key
    // before the app does, so the last letter may not have landed yet when
    // the app is asked. Either the whole prefix, or all of it but the last
    // letter, is accepted. Ask for prefix.Length + 1 characters.
    // Time O(L).
    public static bool ConfirmsPrefix(string? textBeforeCaret, string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return false;
        }

        int requested = prefix.Length + 1;

        if (ConfirmsWord(textBeforeCaret, prefix, requested))
        {
            return true;
        }

        return prefix.Length > 1 && ConfirmsWord(textBeforeCaret, prefix[..^1], requested);
    }
}
