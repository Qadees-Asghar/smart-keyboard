namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Follows the word being typed in any app, one key at a time.
///
/// In System Wide Mode SmartKeyboard cannot read the other app's text. All it
/// ever sees is the keys going past, so it has to build up the current word
/// itself. This class is that memory, and it is plain logic with no Windows in
/// it, so it can be tested properly.
///
/// Privacy note. Nothing here is ever written to a file. The current word is
/// held in memory only, and it is thrown away as soon as the word is finished.
/// </summary>
public class TypedWordTracker
{
    private readonly System.Text.StringBuilder _current = new();

    /// <summary>The word being typed right now.</summary>
    public string CurrentWord => _current.ToString();

    /// <summary>The word finished just before this one, or null.</summary>
    public string? PreviousWord { get; private set; }

    /// <summary>True when nothing is being typed at the moment.</summary>
    public bool IsEmpty => _current.Length == 0;

    /// <summary>Raised when a word is finished, so it can be learned from.</summary>
    public event EventHandler<WordFinishedEventArgs>? WordFinished;

    /// <summary>Raised whenever the word being typed changes.</summary>
    public event EventHandler? CurrentWordChanged;

    // Takes one typed character and updates what we think is being typed.
    // Time O(1), except when a word ends, which is still O(1) on average.
    public void AddCharacter(char c)
    {
        if (WordScanner.IsWordChar(c))
        {
            _current.Append(c);
            CurrentWordChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // A digit means this is a code or a number, not a word worth helping
        // with, so drop what we had and wait for the next real word.
        if (char.IsDigit(c))
        {
            Clear();
            return;
        }

        FinishWord(c);
    }

    // Removes the last letter, the way Backspace does.
    // Returns false when there was nothing left to remove, which tells the
    // caller the user is now deleting text we cannot see.
    // Time O(1).
    public bool Backspace()
    {
        if (_current.Length == 0)
        {
            // We have lost track of the text, so forget the context too.
            Reset();
            return false;
        }

        _current.Length--;
        CurrentWordChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // Forgets the word being typed but keeps the word before it.
    // Used when a word is finished normally. Time O(1).
    public void Clear()
    {
        if (_current.Length == 0)
        {
            return;
        }

        _current.Clear();
        CurrentWordChanged?.Invoke(this, EventArgs.Empty);
    }

    // Forgets everything. Used when the user clicks somewhere, presses an
    // arrow key, or switches to another window, because at that point we have
    // no idea where the caret is any more. Time O(1).
    public void Reset()
    {
        bool hadWord = _current.Length > 0;

        _current.Clear();
        PreviousWord = null;

        if (hadWord)
        {
            CurrentWordChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Replaces the word being typed, after a suggestion was accepted.
    // Time O(L).
    public void AcceptWord(string word)
    {
        _current.Clear();
        PreviousWord = string.IsNullOrWhiteSpace(word) ? null : word.ToLowerInvariant();
        CurrentWordChanged?.Invoke(this, EventArgs.Empty);
    }

    // Ends the current word and remembers it as the previous one.
    // A full stop clears the context, because the next sentence has nothing to
    // do with this one. Time O(1).
    private void FinishWord(char separator)
    {
        string finished = _current.ToString();

        _current.Clear();

        if (finished.Length > 0)
        {
            WordFinished?.Invoke(this, new WordFinishedEventArgs(finished, PreviousWord, separator));
            PreviousWord = finished.ToLowerInvariant();
        }

        if (WordScanner.IsSentenceEnd(separator))
        {
            PreviousWord = null;
        }

        CurrentWordChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Details of a word that was just finished.</summary>
public class WordFinishedEventArgs : EventArgs
{
    public WordFinishedEventArgs(string word, string? previousWord, char separator)
    {
        Word = word;
        PreviousWord = previousWord;
        Separator = separator;
    }

    /// <summary>The word that was finished.</summary>
    public string Word { get; }

    /// <summary>The word before it, or null.</summary>
    public string? PreviousWord { get; }

    /// <summary>The character that ended it, usually a space.</summary>
    public char Separator { get; }
}
