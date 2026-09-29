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

    // Whether the user is in the middle of writing something, as opposed to
    // simply pressing keys. This is what keeps the box off the screen when
    // there is nothing being typed into.
    private readonly TypingSession _session = new();
    private readonly SuggestionPopup _popup = new();
    private readonly Control _uiThread;

    /// <summary>
    /// How long to wait for a typed separator to reach the other app before
    /// correcting the word in front of it. The hook sees keys before the app
    /// does, so without this pause the delete would start too early.
    ///
    /// This was 45, which is fine on a warm machine and not enough on a cold
    /// one. Straight after a restart the other app is still starting up and
    /// takes longer to take the key, so the backspaces began before the space
    /// had landed and ate a character in front of the word instead. Ninety is
    /// still far below anything a person notices, and it doubles the margin.
    /// </summary>
    private const int SeparatorLandingMs = 90;

    // Only the newest lookup is worth showing. This counter lets an older one
    // that finishes late recognise that it has been overtaken, and give up.
    private int _lookupId;

    private IntPtr _lastWindow = IntPtr.Zero;
    private volatile bool _pausedForPrivacy;
    private volatile bool _reportedTypingBlocked;

    // True while the app in front is a code editor or a terminal. Read by the
    // hook thread on every key, so it is a plain field and nothing more.
    private volatile bool _inCodeWindow;

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
        _privacyTimer.Tick += CheckIdle;
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

            if (_inCodeWindow)
            {
                return "Quiet, this is a code editor";
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
        if (IsQuiet())
        {
            // A box left up from the app before, say after switching to the
            // editor with Alt+Tab, would sit on top of the editor's own.
            if (_popupShowing)
            {
                HidePopup();
            }

            return;
        }

        HandleTypedCharacter(e.Character);
    }

    // Follows one typed character and decides what to offer.
    //
    // Separate from the hook event because a letter held back during a
    // replacement is put through here afterwards, and it has to be followed
    // in exactly the same way as one that arrived normally. Time O(1).
    private void HandleTypedCharacter(char character)
    {
        NoticeWindowChange();

        // Only letters count as writing. A space on its own does not start a
        // session, which is what stops a stray space putting a list of next
        // word guesses on an idle screen, while still leaving the session
        // running when a space follows a word that was just typed.
        if (WordScanner.IsWordChar(character))
        {
            _session.NoteKey(DateTime.UtcNow);
        }

        _tracker.AddCharacter(character);

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
        if (IsQuiet())
        {
            return;
        }

        switch (e.Kind)
        {
            // These only mean something while the popup is up. When it is
            // not, the key is left alone so the app underneath behaves as
            // normal. That matters: in a chat app, swallowing Enter when there
            // is nothing to accept would stop the user sending a message.
            // A key is only taken when it is really being used. Setting
            // Handled whatever happened was a bug: when the list on screen no
            // longer matched what had been typed, the word was refused AND the
            // Enter was eaten, so the user got neither a suggestion nor a new
            // line. In an editor, where the popup is up nearly all the time,
            // that meant Enter and the arrow keys simply stopped working.
            case ControlKeyKind.Accept:
                e.Handled = _popupShowing && AcceptFromPopup();
                break;

            case ControlKeyKind.MoveUp:
            case ControlKeyKind.MoveDown:
                if (_popupShowing)
                {
                    e.Handled = MoveSelection(e.Kind == ControlKeyKind.MoveDown ? 1 : -1);
                }
                else
                {
                    _tracker.Reset();
                }

                break;

            case ControlKeyKind.Dismiss:
                _session.End();

                if (_popupShowing)
                {
                    HidePopup();
                    e.Handled = true;
                }

                break;

            case ControlKeyKind.Backspace:
                HandleBackspace();
                break;

            case ControlKeyKind.CaretMoved:
                // We no longer know where the caret is, so forget everything.
                _tracker.Reset();
                _session.End();
                _frozenAnchor = null;
                HidePopup();
                break;
        }
    }

    // Follows one Backspace. Separate from the hook event for the same reason
    // as HandleTypedCharacter: one held back during a replacement is put
    // through here afterwards. Time O(1).
    private void HandleBackspace()
    {
        if (_tracker.Backspace() && !_tracker.IsEmpty)
        {
            RequestSuggestions(_tracker.CurrentWord, _tracker.PreviousWord);
        }
        else
        {
            HidePopup();
        }
    }

    // Moves the highlight, wrapping at the ends. Runs on the hook thread, so
    // the new position is worked out here and only the drawing is handed over.
    // Time O(1).
    private bool MoveSelection(int step)
    {
        string[] words = _current.Words;
        if (words.Length == 0)
        {
            // Nothing to move through, so the arrow key belongs to the app.
            return false;
        }

        int next = (_selectedIndex + step + words.Length) % words.Length;
        _selectedIndex = next;

        if (_uiThread.IsDisposed || !_uiThread.IsHandleCreated)
        {
            return false;
        }

        try
        {
            _uiThread.BeginInvoke(() => _popup.SetSelection(next));
        }
        catch (InvalidOperationException)
        {
            // Closing down.
        }

        return true;
    }

    // A word was finished, so learn from it. Runs on the hook thread, and
    // learning is only counting, so it is quick. Time O(L).
    private void OnWordFinished(object? sender, WordFinishedEventArgs e)
    {
        _services.Learning.RecordWord(e.Word, e.PreviousWord);

        // A word we did not watch from its first letter may be the tail of a
        // longer one, and deleting our count would leave wreckage like
        // "hehello". It is still fixed, but only once the app has shown us
        // that the word on screen really is whole.
        if (_services.Settings.SystemWideAutocorrect)
        {
            TryAutocorrect(e.Word, e.Separator, e.PreviousWord, e.Previous, mustConfirm: !e.StartWasKnown);
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
    // Third, the user may have carried on typing during that wait. If they
    // have, the separator is no longer the last thing in front of the caret,
    // and deleting the word plus one would eat letters belonging to whatever
    // came next. Typing "helo " and going straight on to "world" left "h"
    // behind and produced "hhello". So the tracker's version is noted before
    // the wait and checked after it, and a correction that has gone stale is
    // dropped. AcceptWord guards itself the same way, by prefix.
    //
    // Time O(1) here, the real work is on the background thread.
    //
    // mustConfirm is set for the first word after a click or a window change,
    // whose start we did not see. That word is only replaced if the app
    // reports that the text before the caret is exactly the word and its
    // separator, with a word boundary in front.
    //
    // When the word itself is fine, the word BEFORE it gets a second look,
    // now that both its neighbours are known: "email form the" is fixed to
    // "email from the" as "the" is finished. previous is that word exactly
    // as it sits on screen, or null when we cannot vouch for it.
    private void TryAutocorrect(
        string word, char separator, string? previousWord, FinishedWord? previous, bool mustConfirm)
    {
        AutocorrectEngine? autocorrect = _services.Autocorrect;
        if (autocorrect is null)
        {
            return;
        }

        // Read now, on the hook thread, while this is still the newest thing
        // the user typed.
        int version = _tracker.Version;

        Task.Run(async () =>
        {
            AutocorrectResult result = autocorrect.Check(word, previousWord is null, previousWord);

            // What is on screen now, and what it should become. A typo in
            // this word comes first; only a word that needed no fixing lets
            // the word before it be looked at again.
            string from;
            string to;

            if (result.Changed)
            {
                from = word + separator;
                to = result.Corrected + separator;
            }
            else if (!mustConfirm && TryRealWordFix(previous, word, separator, out from, out to))
            {
                // The earlier word is being swapped. This one is untouched.
            }
            else
            {
                return;
            }

            // Only fix words that went somewhere text can be typed. Letters
            // pressed with the desktop or a file list in focus are not a
            // sentence, and sending backspaces at Explorer would drive its
            // type ahead search or start renaming something.
            CaretLocator.TextTarget target = CaretLocator.DescribeTarget();
            if (!TextInputClassifier.TakesText(target.HasCaret, target.HasFocus, target.ControlClass))
            {
                return;
            }

            // From here until the fix is in, the person's own letters are
            // held back rather than allowed to land in the middle of it.
            //
            // The holding covers the settling wait as well as the typing,
            // not just the typing. Waiting unguarded and then checking was
            // tried first, and it was safe but useless: anyone typing at a
            // normal speed pressed the next letter during the wait, the
            // check saw the text had moved on, and the fix was abandoned.
            // Nothing was mangled, but nothing was corrected either.
            //
            // Holding is only started once there is a real correction to
            // make, so ordinary typing is never delayed.
            //
            // Whatever was held goes back in from the finally block, on every
            // path. A correction called off by the check below used to return
            // without replaying, and the held letters then turned up at the
            // next replacement, somewhere else entirely.
            _hook.Gate.Begin();

            try
            {
                await Task.Delay(SeparatorLandingMs).ConfigureAwait(false);

                // A click, an arrow or a change of window still calls this
                // off. Those are not held, and they mean the caret is
                // somewhere else.
                if (_tracker.Version != version)
                {
                    return;
                }

                // Asked while the person's keys are held, so the caret is not
                // moving under the question.
                if (mustConfirm && !IsWholeWordBeforeCaret(word + separator))
                {
                    return;
                }

                // Delete back to the start of what changes, then type the rest
                // back in, separators included, so the sentence reads the
                // same. Only the letters that differ are actually touched.
                TextInjector.InjectionResult sent = TextInjector.Replace(from, to);

                Log(sent);

                // The tracker is moved on before the held keys are replayed,
                // so they are followed from what is really on screen. A swap
                // of the earlier word leaves this one exactly as it was.
                if (sent.Ok && result.Changed)
                {
                    _tracker.CorrectLastWord(result.Corrected);
                }
                else if (!sent.Ok)
                {
                    ReportTypingBlocked();
                }
            }
            finally
            {
                _hook.Gate.End(ReplayHeld);
            }
        });
    }

    // Asks whether the word before this one was the wrong real word, now
    // that the words on both sides of it are known. When it was, gives the
    // text to replace, from the start of that word up to the caret, and what
    // to put there instead. Time O(1), a few lookups.
    private bool TryRealWordFix(
        FinishedWord? previous, string word, char separator, out string from, out string to)
    {
        from = string.Empty;
        to = string.Empty;

        RealWordChecker checker = _services.RealWords;

        // Only a word we watched from its first letter, and whose record
        // still matches the screen letter for letter, can be gone back over.
        if (!checker.Enabled || previous is null || !previous.StartKnown
            || !RealWordChecker.IsConfusable(previous.Typed))
        {
            return false;
        }

        AutocorrectResult fix = checker.Check(previous.Before, previous.Typed, word);
        if (!fix.Changed)
        {
            return false;
        }

        from = previous.Typed + previous.Separator + word + separator;
        to = fix.Corrected + previous.Separator + word + separator;
        return true;
    }

    // Asks the app what is before the caret, and marks the word start as
    // known if that proves the word being typed is whole. Background threads
    // only. Time: one short, capped question to the other app.
    private bool ConfirmWordStart(string prefix, int version)
    {
        string? before = CaretTextReader.ReadBeforeCaret(prefix.Length + 1);

        return CaretTextCheck.ConfirmsPrefix(before, prefix)
            && _tracker.TryConfirmWordStart(version);
    }

    // True when the app shows exactly this text just before the caret, with a
    // word boundary or the start of the text in front of it. Background
    // threads only. Time: one short, capped question to the other app.
    private static bool IsWholeWordBeforeCaret(string expected)
    {
        int requested = expected.Length + 1;
        string? before = CaretTextReader.ReadBeforeCaret(requested);

        return CaretTextCheck.ConfirmsWord(before, expected, requested);
    }

    // Asks for suggestions away from the hook thread, then shows them on the
    // UI thread. Time O(1) here, the real work is on the other threads.
    private void RequestSuggestions(string prefix, string? previousWord)
    {
        // The cheap half of the decision is made here, on the hook thread,
        // because counting letters costs nothing. Asking Windows what has
        // focus does cost something, so that waits for the other thread.
        if (prefix.Length < SuggestionPolicy.MinPrefixLength)
        {
            HidePopup();
            return;
        }

        // Read here, on the hook thread, where it still describes the word
        // being typed rather than whatever comes after it.
        bool wordStartKnown = _tracker.IsWordStartKnown;
        int version = _tracker.Version;

        int id = System.Threading.Interlocked.Increment(ref _lookupId);

        Task.Run(() =>
        {
            CaretLocator.TextTarget target = CaretLocator.DescribeTarget();
            bool takesText = TextInputClassifier.TakesText(
                target.HasCaret, target.HasFocus, target.ControlClass);

            // The first word after a click or a change of window: we did not
            // see where it starts, so ask the app. If it shows a word
            // boundary right before the letters we followed, the word is
            // whole, and from here on it is treated exactly like any other,
            // so it can be completed now and corrected when it ends.
            if (takesText && !wordStartKnown && ConfirmWordStart(prefix, version))
            {
                wordStartKnown = true;
            }

            SuggestionPolicy.Refusal refusal =
                SuggestionPolicy.CheckCompletions(prefix, takesText, wordStartKnown);

            if (refusal != SuggestionPolicy.Refusal.None)
            {
                LogPopup(refusal, "completion", prefix.Length, 0, target.ControlClass);
                HidePopup();
                return;
            }

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

            LogPopup(
                words.Count == 0 ? SuggestionPolicy.Refusal.NoWords : SuggestionPolicy.Refusal.None,
                "completion",
                prefix.Length,
                words.Count,
                target.ControlClass);

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

        // Read here, on the hook thread, so it reflects the moment the space
        // was pressed rather than whenever the other thread gets round to it.
        bool sessionLive = _session.IsLive(DateTime.UtcNow);

        int id = System.Threading.Interlocked.Increment(ref _lookupId);

        Task.Run(() =>
        {
            CaretLocator.TextTarget target = CaretLocator.DescribeTarget();
            bool takesText = TextInputClassifier.TakesText(
                target.HasCaret, target.HasFocus, target.ControlClass);

            SuggestionPolicy.Refusal refusal =
                SuggestionPolicy.CheckPredictions(takesText, sessionLive);

            if (refusal != SuggestionPolicy.Refusal.None)
            {
                LogPopup(refusal, "prediction", 0, 0, target.ControlClass);
                HidePopup();
                return;
            }

            List<string> words = _services.Predictions.PredictNextWords(
                previousWord, _services.Settings.SuggestionCount);

            if (id != System.Threading.Volatile.Read(ref _lookupId))
            {
                return;
            }

            LogPopup(
                words.Count == 0 ? SuggestionPolicy.Refusal.NoWords : SuggestionPolicy.Refusal.None,
                "prediction",
                0,
                words.Count,
                target.ControlClass);

            // Predictions belong to no prefix: nothing has been typed yet, so
            // accepting one inserts a word rather than replacing one.
            ShowOnUiThread(new SuggestionSet(string.Empty, words.ToArray()));
        });
    }

    // Hands the words to the UI thread, which is the only one allowed to
    // touch the popup window.
    //
    // The caret is looked for here, first, on the worker thread that did the
    // lookup. Finding it can mean asking the other app through UI Automation,
    // and the UI thread must never sit waiting on another program.
    // Time O(1), plus the capped caret question.
    private void ShowOnUiThread(SuggestionSet set)
    {
        if (_uiThread.IsDisposed || !_uiThread.IsHandleCreated)
        {
            return;
        }

        CaretLocator.CaretPosition caret = set.Words.Length == 0
            ? default
            : CaretLocator.Find();

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

                _popup.ShowWords(set.Words, GetPopupPosition(caret), 0);
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
    // 1. The caret, found by CaretLocator. Exact, and it moves as the user
    //    types. Older desktop programs report it to Windows directly;
    //    browsers, Electron apps and the Windows 11 Notepad report nothing
    //    that way, so they are asked through UI Automation for the box of
    //    the character next to the caret instead.
    //
    // 2. Where the user last clicked, for an app that answers neither. This
    //    used to be the answer for every browser, and it was the bug where
    //    the popup sat at the click while the words went in further along
    //    the line. It is only a guess, and now only a last resort.
    //
    // 3. The mouse, only until the first click is seen. Held still for the
    //    rest of the word, because a popup chasing the pointer around is
    //    worse than one slightly out of place.
    // Time O(1).
    private Point GetPopupPosition(CaretLocator.CaretPosition caret)
    {
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

    /// <summary>
    /// Types the highlighted word into the app in front. Returns true when a
    /// word really is going in, so the caller knows whether to keep the key.
    /// </summary>
    // Time O(1) here, the typing itself is handed to another thread.
    public bool AcceptFromPopup()
    {
        SuggestionSet set = _current;
        int index = _selectedIndex;

        return AcceptWord(set, index >= 0 && index < set.Words.Length ? set.Words[index] : null);
    }

    // A row in the popup was clicked. The popup never takes focus, so the app
    // being typed into still has the caret and the word can go straight in.
    // Runs on the UI thread. Time O(n).
    private void OnWordClicked(object? sender, int row)
    {
        _selectedIndex = row;
        AcceptFromPopup();
    }

    // Decides whether a word can go in, and if so starts putting it there.
    //
    // The deciding happens here, on whichever thread asked, so the answer is
    // known at once and the key can be kept or passed on. The typing happens
    // on another thread, because it deliberately pauses between keystrokes
    // and this is sometimes the keyboard hook thread, which must never wait.
    // Time O(1) here.

    private bool AcceptWord(SuggestionSet set, string? word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return false;
        }

        // The same rule as everywhere else: never replace letters we did not
        // watch being typed.
        if (!_tracker.IsWordStartKnown)
        {
            HidePopup();
            return false;
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
            return false;
        }

        string replacement = WordScanner.MatchCapitalization(typed, word);
        string? previous = _tracker.PreviousWord;

        // Committed now, before the typing starts. Moving the tracker on
        // straight away also bumps its version, which cancels any correction
        // that was already in flight for the word being replaced.
        _services.Learning.RecordWord(word, previous);
        _tracker.AcceptWord(word, replacement);
        HidePopup();

        Task.Run(() =>
        {
            TextInjector.InjectionResult sent = ReplaceHoldingKeys(typed, replacement + " ");

            Log(sent);

            if (!sent.Ok)
            {
                ReportTypingBlocked();
            }
        });

        return true;
    }

    // Notes how a replacement went, when the user has asked for that. Counts
    // only, never any text. Time O(1), and nothing at all when it is off.
    private void Log(TextInjector.InjectionResult sent)
    {
        // Checked here, before the process lookup, because this runs while
        // the person's keys are being held and that lookup is not free.
        if (!_services.Settings.Diagnostics)
        {
            return;
        }

        InjectionLog.Record(
            true,
            PrivacyGuard.GetForegroundProcessName(),
            sent);
    }

    // Replaces a word in the app in front, keeping the person's own typing
    // out of the middle of it.
    //
    // Sending backspaces and letters takes a moment, TextInjector.KeyGapMs per
    // key, because some apps drop keys sent in a burst. A key pressed inside
    // that window used to land between ours, which is what
    // produced "wworld" and "wlord". The hook holds any letter typed during
    // the replacement and hands it back here, and it is then typed properly
    // afterwards, so nothing is lost and nothing is scrambled.
    //
    // The hold is ended in a finally block. If it were ever left on, letters
    // would stop reaching the app, so that is not left to chance.
    // Time O(n), TextInjector.KeyGapMs per key sent.
    private TextInjector.InjectionResult ReplaceHoldingKeys(string typed, string replacement)
    {
        _hook.Gate.Begin();

        try
        {
            return TextInjector.Replace(typed, replacement);
        }
        finally
        {
            _hook.Gate.End(ReplayHeld);
        }
    }

    // Puts back whatever the person typed while a replacement was going in.
    // Almost always nothing. Called by the gate with holding still on, so
    // anything typed while this runs waits its turn behind these.
    // Time O(n) over the few keys held.
    private void ReplayHeld(string keys)
    {
        foreach (char c in keys)
        {
            // Sent to the app, and put through the tracker as well, so the
            // word being followed stays in step with what is on screen. The
            // tracker is skipped when quiet, exactly as for a live key.
            bool follow = !IsQuiet();

            if (c == KeyHoldGate.Backspace)
            {
                TextInjector.TypeBackspace();

                if (follow)
                {
                    HandleBackspace();
                }
            }
            else
            {
                TextInjector.TypeCharacter(c);

                if (follow)
                {
                    HandleTypedCharacter(c);
                }
            }
        }
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
        _session.End();
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
        _session.End();
        _frozenAnchor = null;

        // The click that placed the caret belongs to the window we just left.
        _lastClick = null;
    }

    // True when SmartKeyboard should keep completely out of the way, for any
    // reason. Nothing is watched, nothing is suggested, and no key is
    // swallowed, so the app in front behaves exactly as if we were not here.
    // Time O(1).
    //
    // SmartKeyboard's own windows count too. The editor has its own
    // suggestions and its own autocorrect, so watching it from here as well
    // put a second popup on top of the editor's and could fix a word twice.
    private bool IsQuiet()
    {
        return _pausedForPrivacy || _inCodeWindow || IsOwnWindowInFront();
    }

    // True when the window in front belongs to this program: the editor,
    // settings, or the dictionary. Time O(1), two cheap Windows calls.
    private static bool IsOwnWindowInFront()
    {
        IntPtr window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(window, out uint processId);
        return processId == (uint)Environment.ProcessId;
    }

    // Takes the box away once the user has stopped typing.
    //
    // This runs on the timer that was already there for the privacy check, so
    // it costs no extra thread and nothing on the keyboard path. Without it a
    // list of suggestions stays on screen until the next key, which is what
    // made it feel like the box was up whether or not anyone was writing.
    // Time O(1).
    private void CheckIdle(object? sender, EventArgs e)
    {
        if (_popupShowing && !_session.IsLive(DateTime.UtcNow))
        {
            HidePopup();
        }
    }

    // Notes why the box was or was not shown, when the user has asked for a
    // record. Counts and window classes only, never a letter of what was
    // typed: "len" is a length. Time O(1), and nothing at all when off.
    private void LogPopup(
        SuggestionPolicy.Refusal refusal, string kind, int prefixLength, int words, string controlClass)
    {
        InjectionLog.RecordPopup(
            _services.Settings.Diagnostics, refusal, kind, prefixLength, words, controlClass);
    }

    // Looks at whether a password may be on screen. Runs on its own timer,
    // well away from the keyboard hook.
    // Time O(1) plus one short, capped call into the focused program.
    private void CheckPrivacy(object? sender, EventArgs e)
    {
        bool sensitive;
        bool code;

        try
        {
            // A reading that failed tells us nothing, so the old answer is
            // kept rather than flipping the state. Flipping resets the word
            // being typed, and that costs the user a correction for no
            // reason at all.
            sensitive = PrivacyGuard.Look() ?? _pausedForPrivacy;
            code = PrivacyGuard.IsCodeWindow();
        }
        catch (Exception)
        {
            sensitive = true;
            code = false;
        }

        if (sensitive == _pausedForPrivacy && code == _inCodeWindow)
        {
            return;
        }

        bool wasQuiet = IsQuiet();

        _pausedForPrivacy = sensitive;
        _inCodeWindow = code;

        // Whatever was being typed belonged to the window we were watching
        // before, so it is dropped on the way in and on the way out.
        if (IsQuiet() != wasQuiet)
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
