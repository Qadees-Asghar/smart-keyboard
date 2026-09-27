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

    // Goes up every time the text we are following changes. Interlocked
    // because the keyboard hook thread and the thread that types a correction
    // both change it.
    private int _version;

    /// <summary>
    /// Goes up every time the text being followed changes.
    ///
    /// Some work is started now and finished a moment later, most of all a
    /// correction, which has to wait for the user's space to reach the other
    /// app before it can delete anything. By then the user may have typed
    /// more, and the letters it was going to delete are no longer the ones in
    /// front of the caret. Deleting anyway is what turned "helo " into
    /// "hhello". So the slow job notes this number when it starts and checks
    /// it again before touching anything, and gives up if it has moved.
    /// </summary>
    public int Version => System.Threading.Volatile.Read(ref _version);

    /// <summary>
    /// True when we watched the current word from its very first letter, so
    /// the letters we think sit in front of the caret really are the ones
    /// there.
    ///
    /// This is the whole defence against mangled text, and it is worth being
    /// precise about why. We cannot read the other app. The word here is
    /// built from keystrokes alone, and Reset means we just lost track: the
    /// caret moved, the window changed, or keys went past while we were not
    /// watching. If the user was halfway through a word when that happened,
    /// the letters counted from then on are only the tail of what is on
    /// screen. Replacing on that count deletes too few characters, and
    /// "hello" comes back as "hehello".
    ///
    /// So a word is only ever replaced when this is true, and it only becomes
    /// true again once a real word boundary has gone past where we could see
    /// it. The cost is one uncorrected word after losing track. The thing it
    /// buys is that corrupting the line stops being possible, rather than
    /// being something to catch case by case.
    /// </summary>
    public bool IsWordStartKnown { get; private set; }

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
            Bump();
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
        Bump();
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
        Bump();

        // A digit is a boundary, and we saw it go past, so whatever is typed
        // next starts where we think it does.
        IsWordStartKnown = true;

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
        Bump();

        // We no longer know what sits in front of the caret, so nothing may
        // be replaced until a word boundary has been seen again.
        IsWordStartKnown = false;

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
        Bump();

        // We put the word and its space there ourselves, so we know exactly
        // what is in front of the caret.
        IsWordStartKnown = true;

        PreviousWord = string.IsNullOrWhiteSpace(word) ? null : word.ToLowerInvariant();
        CurrentWordChanged?.Invoke(this, EventArgs.Empty);
    }

    // Notes that the text we are following has changed. Time O(1).
    private void Bump()
    {
        System.Threading.Interlocked.Increment(ref _version);
    }

    // Ends the current word and remembers it as the previous one.
    // A full stop clears the context, because the next sentence has nothing to
    // do with this one. Time O(1).
    private void FinishWord(char separator)
    {
        string finished = _current.ToString();

        // Whether THIS word was watched from its first letter. It has to be
        // read before the flag is moved on below, and it has to travel with
        // the event, because a listener that fixes typos does its work a
        // moment later, by which time the flag describes the next word.
        bool startWasKnown = IsWordStartKnown;

        // Counted here, before the event, so a listener that starts slow work
        // captures a number that will not move again until the next key.
        _current.Clear();
        Bump();

        if (finished.Length > 0)
        {
            WordFinished?.Invoke(
                this, new WordFinishedEventArgs(finished, PreviousWord, separator, startWasKnown));

            PreviousWord = finished.ToLowerInvariant();
        }

        // The separator went past where we could see it, so the NEXT word
        // starts exactly where we think it does.
        IsWordStartKnown = true;

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
    public WordFinishedEventArgs(
        string word, string? previousWord, char separator, bool startWasKnown)
    {
        Word = word;
        PreviousWord = previousWord;
        Separator = separator;
        StartWasKnown = startWasKnown;
    }

    /// <summary>The word that was finished.</summary>
    public string Word { get; }

    /// <summary>The word before it, or null.</summary>
    public string? PreviousWord { get; }

    /// <summary>The character that ended it, usually a space.</summary>
    public char Separator { get; }

    /// <summary>
    /// True when this word was watched from its very first letter. When it is
    /// false the word on screen may be longer than the one reported here, so
    /// it must not be replaced.
    /// </summary>
    public bool StartWasKnown { get; }
}
