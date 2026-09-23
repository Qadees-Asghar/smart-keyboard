using SmartKeyboard.Core;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Runs System Wide Mode: the keyboard hook, the popup, and the typing.
///
/// Three threads meet here, and keeping them apart is what makes this work.
///
///   1. The hook thread. It must be quick, because it sits in the middle of
///      every keystroke on the PC. All it does is update the word tracker and
///      ask for a refresh.
///   2. A background thread. Looking words up takes real time, so it happens
///      here, away from the hook.
///   3. The UI thread. Only this one is allowed to touch the popup window, so
///      the answer is handed back with BeginInvoke.
///
/// The word being typed is held in memory only and is never written anywhere.
/// </summary>
public class SystemWideController : IDisposable
{
    private readonly AppServices _services;
    private readonly KeyboardHook _hook = new();
    private readonly MouseHook _mouse = new();
    private readonly TypedWordTracker _tracker = new();
    private readonly SuggestionPopup _popup = new();
    private readonly Control _uiThread;

    /// <summary>
    /// How long to wait for a typed separator to reach the other app before
    /// correcting the word in front of it. The hook sees keys before the app
    /// does, so without this pause the delete would start too early.
    /// </summary>
    private const int SeparatorLandingMs = 45;

    // Only the newest lookup is worth showing. This counter lets an older one
    // that finishes late recognise that it has been overtaken, and give up.
    private int _lookupId;

    private IntPtr _lastWindow = IntPtr.Zero;
    private volatile bool _pausedForPrivacy;
    private volatile bool _reportedTypingBlocked;

    /// <summary>How often to look for a password box, in milliseconds.</summary>
    private const int PrivacyCheckMs = 300;

    private readonly System.Windows.Forms.Timer _privacyTimer = new() { Interval = PrivacyCheckMs };

    // Our own window handles, remembered so they are never mistaken for the
    // app the user is typing into.
    private IntPtr _popupHandle;
    private IntPtr _ownerHandle;

    // The hook thread has to know instantly whether the popup is showing, so
    // it can decide to swallow Enter. Asking the popup itself would mean
    // crossing to the UI thread, which is far too slow inside a hook.
    private volatile bool _popupShowing;

    /// <summary>
    /// The words on screen, together with the exact prefix they were worked
    /// out for. The two are kept in one object on purpose. Held as separate
    /// fields they could disagree for a moment, and then accepting a word
    /// would delete the wrong number of letters and leave wreckage like
    /// "how" turning into "hoa". One reference, swapped in one go, cannot.
    /// </summary>
    private sealed record SuggestionSet(string Prefix, string[] Words)
    {
        public static readonly SuggestionSet Empty = new(string.Empty, Array.Empty<string>());
    }

    // Read by the hook thread on every key, so it is never rebuilt in place.
    private volatile SuggestionSet _current = SuggestionSet.Empty;
    private int _selectedIndex;

    // Where the popup sits when the app does not report a caret. It is worked
    // out once per word and then held still, otherwise the popup would chase
    // the mouse pointer around while the user types.
    private Point? _frozenAnchor;

    // Where the user last clicked. In an app that reports no caret this is by
    // far the best guess at where the text is, because clicking into the box
    // is how you got there. It only changes when you click, so the popup
    // stays put while you type instead of following the pointer.
    private Point? _lastClick;

    /// <summary>How far under the click the popup sits, about one line.</summary>
    private const int LineHeight = 22;

    public SystemWideController(AppServices services, Control uiThread)
    {
        _services = services;
        _uiThread = uiThread;

        _hook.KeyTyped += OnKeyTyped;
        _hook.ControlKeyPressed += OnControlKey;
        _tracker.WordFinished += OnWordFinished;
        _privacyTimer.Tick += CheckPrivacy;
        _mouse.Clicked += OnMouseClicked;
        _popup.WordClicked += OnWordClicked;
        _popup.RowHovered += (_, row) => _selectedIndex = row;
    }

    /// <summary>Raised when the running or paused state changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Raised the first time Windows refuses to let us type into another app.
    /// The usual cause is that app running as Administrator while
    /// SmartKeyboard is not, and Windows blocks input going upwards.
    /// </summary>
    public event EventHandler? TypingBlocked;

    /// <summary>True while keys are being watched.</summary>
    public bool IsRunning => _hook.IsRunning;

    /// <summary>True when watching is switched off by the user.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>True when watching stopped on its own because of a password box.</summary>
    public bool IsPausedForPrivacy => _pausedForPrivacy;

    /// <summary>A short line for the tray tooltip.</summary>
    public string StatusText
    {
        get
        {
            if (!IsRunning)
            {
                return "System Wide Mode is off";
            }

            if (IsPaused)
            {
                return "Paused. Ctrl+Alt+K turns it back on";
            }

            if (_pausedForPrivacy)
            {
                return "Paused, a password box has focus";
            }

            // Whether typos get fixed is said out loud, because it starts off
            // and a tick box in a menu is easy to miss.
            return _services.Settings.SystemWideAutocorrect
                ? "Watching. Typo fixing is on"
                : "Watching. Typo fixing is off";
        }
    }

    // Starts watching the keyboard. Time O(1).
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        // Created now, on the UI thread, so the hook thread can compare
        // against them later without touching a Windows Forms object.
        _ownerHandle = _uiThread.Handle;
        _popupHandle = _popup.Handle;

        _hook.Start();
        _mouse.Start();
        _privacyTimer.Start();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Stops watching and hides the popup. Time O(1).
    public void Stop()
    {
        _hook.Stop();
        _mouse.Stop();
        _privacyTimer.Stop();
        _tracker.Reset();
        HidePopup();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Switches between paused and watching. The Ctrl+Alt+K hotkey.</summary>
    // Time O(1).
    public void TogglePause()
    {
        IsPaused = !IsPaused;
        _hook.IsPaused = IsPaused;
        _mouse.IsPaused = IsPaused;

        if (IsPaused)
        {
            _tracker.Reset();
            HidePopup();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // A key that produced a character. Runs on the hook thread, so it stays
    // short. Time O(1).
    private void OnKeyTyped(object? sender, TypedKeyEventArgs e)
    {
        if (PausedForPrivacy())
        {
            return;
        }

        NoticeWindowChange();
        _tracker.AddCharacter(e.Character);

        if (_tracker.IsEmpty)
        {
            // The word just ended, so offer what usually comes next, and let
            // the next word find its own place on screen.
            _frozenAnchor = null;
            RequestPredictions(_tracker.PreviousWord);
            return;
        }

        RequestSuggestions(_tracker.CurrentWord, _tracker.PreviousWord);
    }

    // A key that did not produce a character. Runs on the hook thread.
    // Time O(1).
    private void OnControlKey(object? sender, ControlKeyEventArgs e)
    {
        if (PausedForPrivacy())
        {
            return;
        }

        switch (e.Kind)
        {
            // These only mean something while the popup is up. When it is
            // not, the key is left alone so the app underneath behaves as
            // normal. That matters: in a chat app, swallowing Enter when there
            // is nothing to accept would stop the user sending a message.
            case ControlKeyKind.Accept:
                if (_popupShowing)
                {
                    AcceptFromPopup();
                    e.Handled = true;
                }

                break;

            case ControlKeyKind.MoveUp:
            case ControlKeyKind.MoveDown:
                if (_popupShowing)
                {
                    MoveSelection(e.Kind == ControlKeyKind.MoveDown ? 1 : -1);
                    e.Handled = true;
                }
                else
                {
                    _tracker.Reset();
                }

                break;

            case ControlKeyKind.Dismiss:
                if (_popupShowing)
                {
                    HidePopup();
                    e.Handled = true;
                }

                break;

            case ControlKeyKind.Backspace:
                if (_tracker.Backspace() && !_tracker.IsEmpty)
                {
                    RequestSuggestions(_tracker.CurrentWord, _tracker.PreviousWord);
                }
                else
                {
                    HidePopup();
                }

                break;

            case ControlKeyKind.CaretMoved:
                // We no longer know where the caret is, so forget everything.
                _tracker.Reset();
                _frozenAnchor = null;
                HidePopup();
                break;
        }
    }

    // Moves the highlight, wrapping at the ends. Runs on the hook thread, so
    // the new position is worked out here and only the drawing is handed over.
    // Time O(1).
    private void MoveSelection(int step)
    {
        string[] words = _current.Words;
        if (words.Length == 0)
        {
            return;
        }

        int next = (_selectedIndex + step + words.Length) % words.Length;
        _selectedIndex = next;

        if (_uiThread.IsDisposed || !_uiThread.IsHandleCreated)
        {
            return;
        }

        try
        {
            _uiThread.BeginInvoke(() => _popup.SetSelection(next));
        }
        catch (InvalidOperationException)
        {
            // Closing down.
        }
    }

    // A word was finished, so learn from it. Runs on the hook thread, and
    // learning is only counting, so it is quick. Time O(L).
    private void OnWordFinished(object? sender, WordFinishedEventArgs e)
    {
        _services.Learning.RecordWord(e.Word, e.PreviousWord);

        if (_services.Settings.SystemWideAutocorrect)
        {
            TryAutocorrect(e.Word, e.Separator, e.PreviousWord is null);
        }
    }

    // Fixes a finished word in the other app, if the engine is sure enough.
    //
    // Two things here are easy to get wrong.
    //
    // First, the check itself searches the BK tree, which is allowed up to
    // 200 ms. Doing that on the hook thread would stall every keystroke on
    // the PC, so it runs on a background thread instead.
    //
    // Second, the separator key has NOT reached the other app yet. The hook
    // sees a key before the app it is going to, so the space the user just
    // pressed is still in flight. Deleting straight away would remove one
    // character too few. A short wait lets it land first.
    //
    // Time O(1) here, the real work is on the background thread.
    private void TryAutocorrect(string word, char separator, bool isSentenceStart)
    {
        AutocorrectEngine? autocorrect = _services.Autocorrect;
        if (autocorrect is null)
        {
            return;
        }

        Task.Run(async () =>
        {
            AutocorrectResult result = autocorrect.Check(word, isSentenceStart);
            if (!result.Changed)
            {
                return;
            }

            await Task.Delay(SeparatorLandingMs).ConfigureAwait(false);

            // Delete the word and the separator, then type the fixed word and
            // put the separator back, so the sentence reads the same.
            if (TextInjector.ReplaceWord(word + separator, result.Corrected + separator))
            {
                _tracker.AcceptWord(result.Corrected);
            }
            else
            {
                ReportTypingBlocked();
            }
        });
    }

    // Asks for suggestions away from the hook thread, then shows them on the
    // UI thread. Time O(1) here, the real work is on the other threads.
    private void RequestSuggestions(string prefix, string? previousWord)
    {
        if (prefix.Length < 1)
        {
            HidePopup();
            return;
        }

        int id = System.Threading.Interlocked.Increment(ref _lookupId);

        Task.Run(() =>
        {
            List<string> words = _services.Suggestions
                .GetSuggestions(prefix, previousWord, _services.Settings.SuggestionCount)
                .Select(w => w.Word)
                .Where(w => !string.Equals(w, prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // A newer keystroke has already overtaken this one.
            if (id != System.Threading.Volatile.Read(ref _lookupId))
            {
                return;
            }

            ShowOnUiThread(new SuggestionSet(prefix, words.ToArray()));
        });
    }

    // Offers the words most likely to follow the one just finished.
    // Runs off the hook thread, the same as a normal lookup.
    // Time O(1) here.
    private void RequestPredictions(string? previousWord)
    {
        if (!_services.Settings.ShowPredictions || string.IsNullOrEmpty(previousWord))
        {
            HidePopup();
            return;
        }

        int id = System.Threading.Interlocked.Increment(ref _lookupId);

        Task.Run(() =>
        {
            List<string> words = _services.Predictions.PredictNextWords(
                previousWord, _services.Settings.SuggestionCount);

            if (id != System.Threading.Volatile.Read(ref _lookupId))
            {
                return;
            }

            // Predictions belong to no prefix: nothing has been typed yet, so
            // accepting one inserts a word rather than replacing one.
            ShowOnUiThread(new SuggestionSet(string.Empty, words.ToArray()));
        });
    }

    // Hands the words to the UI thread, which is the only one allowed to
    // touch the popup window. Time O(1).
    private void ShowOnUiThread(SuggestionSet set)
    {
        if (_uiThread.IsDisposed || !_uiThread.IsHandleCreated)
        {
            return;
        }

        try
        {
            _current = set;
            _selectedIndex = 0;

            _uiThread.BeginInvoke(() =>
            {
                if (set.Words.Length == 0)
                {
                    _popup.HidePopup();
                    _popupShowing = false;
                    return;
                }

                _popup.ShowWords(set.Words, GetPopupPosition(), 0);
                _popupShowing = true;
            });
        }
        catch (InvalidOperationException)
        {
            // The window went away while we were working. Nothing to do.
        }
    }

    // Time O(1).
    private void HidePopup()
    {
        if (_uiThread.IsDisposed || !_uiThread.IsHandleCreated)
        {
            return;
        }

        _popupShowing = false;
        _current = SuggestionSet.Empty;
        _selectedIndex = 0;

        try
        {
            _uiThread.BeginInvoke(() => _popup.HidePopup());
        }
        catch (InvalidOperationException)
        {
            // Closing down.
        }
    }

    // Works out where the popup should appear.
    //
    // Three answers, best first.
    //
    // 1. The caret, when the app reports one. That is exact, and it moves as
    //    the user types. Notepad, Word and most desktop programs do this.
    //
    // 2. Where the user last clicked. Browsers and Electron apps report no
    //    caret at all, and asking Windows harder does not help: their caret
    //    object answers with zeroes and accessibility hands back the whole
    //    window. But clicking into the box is how the user got there, so the
    //    click is near the text. It only changes when they click again, so
    //    the popup holds still while they type.
    //
    // 3. The mouse, only until the first click is seen. Held still for the
    //    rest of the word, because a popup chasing the pointer around is
    //    worse than one slightly out of place.
    // Time O(1).
    private Point GetPopupPosition()
    {
        CaretLocator.CaretPosition caret = CaretLocator.Find();

        if (caret.IsRealCaret)
        {
            _frozenAnchor = null;
            return caret.Location;
        }

        if (_lastClick is Point click)
        {
            return new Point(click.X, click.Y + LineHeight);
        }

        _frozenAnchor ??= caret.Location;
        return _frozenAnchor.Value;
    }

    /// <summary>Types the highlighted word into the app in front.</summary>
    // Time O(n) over the letters replaced.
    public void AcceptFromPopup()
    {
        SuggestionSet set = _current;
        int index = _selectedIndex;

        AcceptWord(set, index >= 0 && index < set.Words.Length ? set.Words[index] : null);
    }

    // A row in the popup was clicked. The popup never takes focus, so the app
    // being typed into still has the caret and the word can go straight in.
    // Runs on the UI thread. Time O(n).
    private void OnWordClicked(object? sender, int row)
    {
        _selectedIndex = row;
        AcceptFromPopup();
    }

    // Time O(n).
    private void AcceptWord(SuggestionSet set, string? word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return;
        }

        string typed = _tracker.CurrentWord;

        // The list on screen was worked out for a particular prefix. If the
        // user has typed, deleted or clicked since then, that prefix is no
        // longer what is in front of the caret, and deleting its length would
        // eat letters that belong to something else. Refuse instead: a
        // suggestion not taken is a small annoyance, mangled text is not.
        if (!string.Equals(typed, set.Prefix, StringComparison.Ordinal))
        {
            HidePopup();
            return;
        }

        string replacement = WordScanner.MatchCapitalization(typed, word);

        if (TextInjector.ReplaceWord(typed, replacement + " "))
        {
            _services.Learning.RecordWord(word, _tracker.PreviousWord);
            _tracker.AcceptWord(word);
        }
        else
        {
            ReportTypingBlocked();
        }

        HidePopup();
    }

    // Says once, and only once, that Windows would not let us type. Saying it
    // every time would be a stream of popups in an app we can never write to.
    // Time O(1).
    private void ReportTypingBlocked()
    {
        if (_reportedTypingBlocked)
        {
            return;
        }

        _reportedTypingBlocked = true;
        TypingBlocked?.Invoke(this, EventArgs.Empty);
    }

    // A click puts the caret somewhere we did not put it, so whatever we
    // thought was being typed is no longer in front of it. Everything is
    // forgotten, exactly as if the user had pressed an arrow key.
    //
    // A click on our own popup is the one exception. That is the user picking
    // a word, and the popup handles it. Forgetting the word first would leave
    // nothing to replace.
    // Time O(1).
    private void OnMouseClicked(object? sender, Point where)
    {
        if (IsOurPopup(NativeMethods.WindowFromPoint(where)))
        {
            return;
        }

        _lastClick = where;
        _tracker.Reset();
        _frozenAnchor = null;
        HidePopup();
    }

    // True when a window is the popup, or something drawn inside it.
    // Time O(1).
    private bool IsOurPopup(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        return window == _popupHandle
            || NativeMethods.GetAncestor(window, NativeMethods.GA_ROOT) == _popupHandle;
    }

    // Notices when the user has moved to a different window, because the word
    // they were typing belongs to the old one.
    //
    // This compares the window handle, not the title. The title was tried
    // first and was a real bug: browsers and chat apps change their title
    // while you type, so the tracker was reset in the middle of a word and
    // the first letter went missing. Typing "goo" then asked for words
    // starting with "oo". A handle does not change while a window is open.
    // Time O(1).
    private void NoticeWindowChange()
    {
        IntPtr window = NativeMethods.GetForegroundWindow();

        // Our own popup and tray window are not a change of app. If they were
        // counted, showing the popup would wipe the word that caused it.
        if (window == IntPtr.Zero || window == _popupHandle || window == _ownerHandle)
        {
            return;
        }

        if (window == _lastWindow)
        {
            return;
        }

        _lastWindow = window;
        _tracker.Reset();
        _frozenAnchor = null;

        // The click that placed the caret belongs to the window we just left.
        _lastClick = null;
    }

    // Reads the cached answer. This runs on the hook thread, so it must be a
    // plain field read and nothing more.
    //
    // It used to ask Windows on every single keystroke, which meant a blocking
    // call into another program in the middle of the input path. That was slow
    // and, worse, browsers answered nonsense, so the word being typed was
    // thrown away on every key. The checking now happens on a timer instead.
    // Time O(1).
    private bool PausedForPrivacy()
    {
        return _pausedForPrivacy;
    }

    // Looks at whether a password may be on screen. Runs on its own timer,
    // well away from the keyboard hook.
    // Time O(1) plus one short, capped call into the focused program.
    private void CheckPrivacy(object? sender, EventArgs e)
    {
        bool sensitive;

        try
        {
            sensitive = PrivacyGuard.ShouldPause();
        }
        catch (Exception)
        {
            sensitive = true;
        }

        if (sensitive == _pausedForPrivacy)
        {
            return;
        }

        _pausedForPrivacy = sensitive;

        if (sensitive)
        {
            _tracker.Reset();
            HidePopup();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _privacyTimer.Dispose();
        _hook.Dispose();
        _mouse.Dispose();
        _popup.Dispose();
        GC.SuppressFinalize(this);
    }
}
