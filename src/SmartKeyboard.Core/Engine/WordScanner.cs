namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Works out which word the user is typing and which word came before it.
/// Both Editor Mode and System Wide Mode need this, so it lives in Core
/// and knows nothing about text boxes or Windows.
///
/// A word is letters plus apostrophes inside it, so "don't" counts as one word.
/// </summary>
public static class WordScanner
{
    /// <summary>Characters that end a sentence and clear the context.</summary>
    public static readonly char[] SentenceEnders = { '.', '?', '!' };

    // True when the character can be part of a word. Time O(1).
    public static bool IsWordChar(char c)
    {
        return char.IsLetter(c) || c == '\'';
    }

    // True when the character finishes a word, so autocorrect should run.
    //
    // Control characters are keys, not text. Backspace, Escape, Tab and Enter
    // all arrive as characters, and counting them as "the word is finished"
    // was a real bug: pressing Backspace to fix a typo made autocorrect change
    // the word first, so the user could never repair it by hand.
    // Time O(1).
    public static bool IsWordSeparator(char c)
    {
        if (char.IsControl(c))
        {
            return false;
        }

        return !IsWordChar(c) && !char.IsDigit(c);
    }

    // True when the character ends a sentence. Time O(1).
    public static bool IsSentenceEnd(char c)
    {
        return c == '.' || c == '?' || c == '!';
    }

    // Reads backwards from the caret to find the word being typed right now.
    // Returns an empty string when the caret is not inside a word.
    // Time O(W) where W is the length of that word.
    public static string GetCurrentPrefix(string? text, int caretIndex)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        caretIndex = Math.Clamp(caretIndex, 0, text.Length);
        int start = GetCurrentWordStart(text, caretIndex);
        return text.Substring(start, caretIndex - start);
    }

    // Finds the position where the word being typed starts.
    // Time O(W).
    public static int GetCurrentWordStart(string? text, int caretIndex)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        caretIndex = Math.Clamp(caretIndex, 0, text.Length);
        int start = caretIndex;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        return start;
    }

    // Finds the finished word that comes before the word being typed.
    // Returns null when there is none, or when a sentence ended in between,
    // because after a full stop the previous sentence gives no useful context.
    // Time O(W) plus the number of separator characters skipped.
    public static string? GetPreviousWord(string? text, int caretIndex)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        caretIndex = Math.Clamp(caretIndex, 0, text.Length);
        int index = GetCurrentWordStart(text, caretIndex) - 1;

        // Step back over spaces and punctuation. A full stop clears the context.
        while (index >= 0 && !IsWordChar(text[index]))
        {
            if (IsSentenceEnd(text[index]))
            {
                return null;
            }

            index--;
        }

        if (index < 0)
        {
            return null;
        }

        int end = index + 1;
        while (index >= 0 && IsWordChar(text[index]))
        {
            index--;
        }

        string word = text.Substring(index + 1, end - index - 1);
        return word.Length == 0 ? null : word.ToLowerInvariant();
    }

    // Copies the shape of the original word onto a replacement word.
    // "Teh" fixed to "the" becomes "The". "TEH" becomes "THE".
    // Time O(L).
    public static string MatchCapitalization(string original, string replacement)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(replacement))
        {
            return replacement;
        }

        // All capitals, and more than one letter, means keep it all capitals.
        if (original.Length > 1 && original.All(c => !char.IsLetter(c) || char.IsUpper(c)))
        {
            return replacement.ToUpperInvariant();
        }

        if (char.IsUpper(original[0]))
        {
            return char.ToUpperInvariant(replacement[0]) + replacement.Substring(1);
        }

        return replacement;
    }
}
