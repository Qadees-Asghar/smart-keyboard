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
    public bool IsWordStartKnown
    {
        get => _wordStartKnown;
        private set => _wordStartKnown = value;
    }

    // Written by the keyboard hook thread, and by a background thread that
    // has confirmed the word start by reading the other app.
    private volatile bool _wordStartKnown;

    // Makes "forget the word start" and "confirm the word start" happen one
    // at a time, so a confirmation can never land just after a Reset and
    // bring back a start that no longer holds.
    private readonly object _startLock = new();

    /// <summary>
    /// Marks the current word as watched from its first letter, because
    /// someone has checked what is on screen and found a word boundary right
    /// before it. Only done when nothing has changed since the check began:
    /// <paramref name="version"/> is the Version read before looking.
    ///
    /// This is what lets the very first word after a click or a window
    /// change be completed and corrected. Without it, that word is always
    /// skipped, because we cannot know it is not the tail of a longer one.
    /// </summary>
    // Time O(1).
    public bool TryConfirmWordStart(int version)
    {
        lock (_startLock)
        {
            if (Version != version)
            {
                return false;
            }

            IsWordStartKnown = true;
            return true;
        }
    }

    /// <summary>
    /// The last word finished, exactly as it sits on screen, with the
    /// character that ended it. Null when we cannot vouch for the text
    /// between it and the caret: after a reset, a sentence end, a digit, or
    /// two separators in a row.
    ///
    /// Fixing a real word mixup ("email form the") means going back over the
    /// word before the one just typed, so it has to be known letter for
    /// letter, along with exactly what sits between the two.
    /// </summary>
    public FinishedWord? LastFinished { get; private set; }

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
        // A digit now sits on screen after the last word, which the record
        // of that word does not know about.
        LastFinished = null;

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
        LastFinished = null;

        // We no longer know what sits in front of the caret, so nothing may
        // be replaced until a word boundary has been seen again, or until
        // TryConfirmWordStart has found one on screen.
        lock (_startLock)
        {
            Bump();
            IsWordStartKnown = false;
        }

        if (hadWord)
        {
            CurrentWordChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Replaces the word being typed, after a suggestion was accepted.
    // onScreen is the word as it was actually typed into the app, with its
    // capitals, when that differs from the dictionary word; a space always
    // follows it. Time O(L).
    public void AcceptWord(string word, string? onScreen = null)
    {
        _current.Clear();
        Bump();

        // We put the word and its space there ourselves, so we know exactly
        // what is in front of the caret.
        IsWordStartKnown = true;

        string? before = PreviousWord;
        PreviousWord = string.IsNullOrWhiteSpace(word) ? null : word.ToLowerInvariant();
        LastFinished = PreviousWord is null ? null : new FinishedWord(onScreen ?? word, ' ', true, before);

        CurrentWordChanged?.Invoke(this, EventArgs.Empty);
    }

    // The word just finished was replaced by a correction, which kept its
    // separator. Everything else about it stays as it was. Time O(L).
    public void CorrectLastWord(string corrected)
    {
        _current.Clear();
        Bump();

        // We put the fixed word there ourselves, so the caret is exactly
        // where we think it is.
        IsWordStartKnown = true;

        // No record means the word ended a sentence, so it is not the
        // context for the next word and must not become it now.
        if (LastFinished is not null && !string.IsNullOrWhiteSpace(corrected))
        {
            PreviousWord = corrected.ToLowerInvariant();
            LastFinished = LastFinished with { Typed = corrected, StartKnown = true };
        }

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

        FinishedWord? previous = LastFinished;

        if (finished.Length > 0)
        {
            WordFinished?.Invoke(
                this,
                new WordFinishedEventArgs(finished, PreviousWord, separator, startWasKnown, previous));

            LastFinished = new FinishedWord(finished, separator, startWasKnown, PreviousWord);
            PreviousWord = finished.ToLowerInvariant();
        }
        else
        {
            // A second separator in a row, like the space in ", ". The record
            // only holds one character after the word, so it no longer
            // describes what is on screen.
            LastFinished = null;
        }

        // The separator went past where we could see it, so the NEXT word
        // starts exactly where we think it does.
        IsWordStartKnown = true;

        if (WordScanner.IsSentenceEnd(separator))
        {
            PreviousWord = null;
            LastFinished = null;
        }

        CurrentWordChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Details of a word that was just finished.</summary>
public class WordFinishedEventArgs : EventArgs
{
    public WordFinishedEventArgs(
        string word,
        string? previousWord,
        char separator,
        bool startWasKnown,
        FinishedWord? previous = null)
    {
        Word = word;
        PreviousWord = previousWord;
        Separator = separator;
        StartWasKnown = startWasKnown;
        Previous = previous;
    }

    /// <summary>
    /// The word before this one exactly as it sits on screen, when we can
    /// vouch for it and for what lies between the two. Null otherwise.
    /// </summary>
    public FinishedWord? Previous { get; }

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

/// <summary>
/// A finished word exactly as it sits on screen.
/// </summary>
/// <param name="Typed">The letters as typed, capitals and all.</param>
/// <param name="Separator">The one character that ended it.</param>
/// <param name="StartKnown">True when it was watched from its first letter.</param>
/// <param name="Before">The word before it, lower case, or null.</param>
public sealed record FinishedWord(string Typed, char Separator, bool StartKnown, string? Before);
