using SmartKeyboard.Core;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.App.Editor;

/// <summary>
/// Editor Mode. A text box that suggests words while you type, marks unknown
/// words in red, and offers fixes when you right click one.
/// </summary>
public class MainForm : Form
{
    private readonly AppServices _services;
    private readonly RichTextBox _textBox;
    private readonly SuggestionList _suggestions;
    private readonly SpellCheckPainter _painter;
    private readonly ContextMenuStrip _wordMenu;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _timingLabel;
    private ToolStripMenuItem _autocorrectItem = new();


    // The word that was right clicked, so the menu knows what to fix.
    private string _clickedWord = string.Empty;
    private int _clickedStart;

    // The last word autocorrect changed, kept so Ctrl+Z can put it back.
    private string _undoOriginal = string.Empty;
    private string _undoCorrected = string.Empty;
    private int _undoStart = -1;

    public MainForm(AppServices services)
    {
        _services = services;

        Text = "SmartKeyboard";
        Width = 900;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        BackColor = BrandTheme.Light;
        ForeColor = BrandTheme.Dark;

        _textBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = BrandTheme.Text(14F),
            BorderStyle = BorderStyle.None,
            BackColor = BrandTheme.Light,
            ForeColor = BrandTheme.Dark,
            AcceptsTab = false,
            HideSelection = false
        };

        // A little breathing room around the writing area.
        _textBox.SelectionIndent = 16;
        _textBox.SelectionRightIndent = 16;

        _suggestions = new SuggestionList();
        _painter = new SpellCheckPainter(_textBox);
        _painter.UseMatcher(() => _services.Fuzzy);
        _painter.Enabled = _services.Settings.SpellCheckEnabled;

        _wordMenu = new ContextMenuStrip
        {
            Font = BrandTheme.Ui(9.5F),
            BackColor = BrandTheme.Light,
            ForeColor = BrandTheme.Dark
        };

        _wordMenu.Opening += OnWordMenuOpening;
        _textBox.ContextMenuStrip = _wordMenu;

        _statusLabel = new ToolStripStatusLabel(BuildStatusText())
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = BrandTheme.Dark
        };
        _timingLabel = new ToolStripStatusLabel("ready") { ForeColor = BrandTheme.Dark };

        var statusBar = new StatusStrip
        {
            Font = BrandTheme.Ui(9F),
            BackColor = BrandTheme.LightGray,
            ForeColor = BrandTheme.Dark,
            SizingGrip = false
        };

        statusBar.Items.Add(_statusLabel);
        statusBar.Items.Add(_timingLabel);

        MenuStrip menuBar = BuildMenuBar();

        _textBox.TextChanged += OnTextChanged;
        _textBox.KeyDown += OnTextBoxKeyDown;
        _textBox.KeyPress += OnTextBoxKeyPress;
        _textBox.MouseDown += OnTextBoxMouseDown;
        _suggestions.Click += (_, _) => AcceptSelectedSuggestion();

        _services.SpellCheckReady += OnSpellCheckReady;

        Controls.Add(_textBox);
        Controls.Add(_suggestions);
        Controls.Add(statusBar);
        Controls.Add(menuBar);
        MainMenuStrip = menuBar;

        _suggestions.BringToFront();
    }

    // Builds the menu at the top of the window. Time O(1).
    private MenuStrip BuildMenuBar()
    {
        var openDictionary = new ToolStripMenuItem("My Dictionary...")
        {
            ShortcutKeys = Keys.Control | Keys.D
        };

        openDictionary.Click += (_, _) => ShowDictionaryWindow();

        var openSettings = new ToolStripMenuItem("Settings...")
        {
            ShortcutKeys = Keys.Control | Keys.Oemcomma
        };

        openSettings.Click += (_, _) => ShowSettingsWindow();

        // A quick switch for the one option people flick on and off the most.
        _autocorrectItem = new ToolStripMenuItem("Autocorrect on space")
        {
            CheckOnClick = true,
            Checked = _services.Settings.AutocorrectEnabled
        };

        _autocorrectItem.CheckedChanged += (_, _) =>
        {
            _services.Settings.AutocorrectEnabled = _autocorrectItem.Checked;
            _services.ApplySettings();
            _services.Settings.Save();
        };

        var resetLearning = new ToolStripMenuItem("Reset what has been learned");
        resetLearning.Click += (_, _) => ResetLearning();

        var tools = new ToolStripMenuItem("Tools");
        tools.DropDownItems.Add(openDictionary);
        tools.DropDownItems.Add(openSettings);
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add(_autocorrectItem);
        tools.DropDownItems.Add(resetLearning);

        var menuBar = new MenuStrip
        {
            Font = BrandTheme.Ui(9.5F),
            BackColor = BrandTheme.Light,
            ForeColor = BrandTheme.Dark,
            Padding = new Padding(8, 4, 0, 4)
        };

        menuBar.Items.Add(tools);
        return menuBar;
    }

    // Opens the Settings window and applies whatever the user chose.
    private void ShowSettingsWindow()
    {
        using var window = new SettingsForm(_services.Settings);

        if (window.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _services.Settings.CopyFrom(window.Result);
        _services.Settings.Save();
        _services.ApplySettings();

        _painter.Enabled = _services.Settings.SpellCheckEnabled;

        // Keep the quick switch in the menu showing the truth.
        _autocorrectItem.Checked = _services.Settings.AutocorrectEnabled;

        _painter.Repaint();
        RefreshSuggestions();
        _statusLabel.Text = "Settings saved. " + BuildStatusText();
    }

    // Opens the window for seeing and editing the user's own words.
    private void ShowDictionaryWindow()
    {
        using var window = new DictionaryForm(_services.Users);
        window.WordsChanged += (_, _) => _painter.Repaint();
        window.ShowDialog(this);
        _painter.Repaint();
    }

    // Builds the one line of text in the status bar. Time O(1).
    private string BuildStatusText()
    {
        string spellCheck = _services.IsSpellCheckReady
            ? $"spell check ready in {_services.TreeBuildMs} ms"
            : "spell check loading";

        string fonts = BrandTheme.UsingBrandFonts
            ? "Poppins and Lora"
            : $"{BrandTheme.UiFamily.Name} and {BrandTheme.TextFamily.Name} (brand fonts not found)";

        return $"{_services.Dictionary.Words.WordCount:N0} words, " +
               $"{_services.Dictionary.Bigrams.PairCount:N0} word pairs, " +
               $"loaded in {_services.Dictionary.LoadTimeMs} ms, {spellCheck}, {fonts}";
    }

    // Runs on a background thread when the typo tree is finished, so the work
    // is handed back to the UI thread before touching any control.
    private void OnSpellCheckReady(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() =>
        {
            _statusLabel.Text = BuildStatusText();
            _painter.Repaint();
        });
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        // Recolouring words raises TextChanged too. That is not the user
        // typing, so ignore it, or the suggestion list would vanish every time
        // the spell check ran.
        if (_painter.IsRepainting)
        {
            return;
        }

        RefreshSuggestions();
        _painter.RequestRepaint();
    }

    private void OnTextBoxMouseDown(object? sender, MouseEventArgs e)
    {
        _suggestions.HideList();

        // A right click must move the caret first, otherwise the menu would
        // act on wherever the caret happened to be before.
        if (e.Button == MouseButtons.Right)
        {
            _textBox.SelectionStart = _textBox.GetCharIndexFromPosition(e.Location);
            _textBox.SelectionLength = 0;
        }
    }

    // Handles the keys that drive the suggestion list before the text box
    // gets a chance to type them. Time O(1).
    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+Z straight after an autocorrect puts the original word back.
        // This has to run whether or not the suggestion list is showing.
        if (e.Control && e.KeyCode == Keys.Z && UndoAutocorrect())
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            return;
        }

        // Enter is no longer counted as a word separator, so finishing a line
        // has to ask for the autocorrect check itself.
        if (e.KeyCode == Keys.Enter && !_suggestions.Visible)
        {
            RunAutocorrectOnFinishedWord();
        }

        if (!_suggestions.Visible)
        {
            return;
        }

        switch (e.KeyCode)
        {
            // Enter is the accept key. Tab still works for anyone used to it.
            // Press Esc first if you want Enter to make a new line instead.
            case Keys.Enter:
            case Keys.Tab:
                AcceptSelectedSuggestion();
                e.SuppressKeyPress = true;
                e.Handled = true;
                break;

            case Keys.Down:
                _suggestions.MoveSelection(1);
                e.SuppressKeyPress = true;
                e.Handled = true;
                break;

            case Keys.Up:
                _suggestions.MoveSelection(-1);
                e.SuppressKeyPress = true;
                e.Handled = true;
                break;

            case Keys.Escape:
                _suggestions.HideList();
                e.SuppressKeyPress = true;
                e.Handled = true;
                break;
        }
    }

    // Looks at what is being typed and shows the top suggestions under it.
    // Time is the same as SuggestionEngine.GetSuggestions.
    private void RefreshSuggestions()
    {
        string prefix = WordScanner.GetCurrentPrefix(_textBox.Text, _textBox.SelectionStart);

        if (prefix.Length < 1)
        {
            ShowNextWordPredictions();
            return;
        }

        string? previousWord = WordScanner.GetPreviousWord(_textBox.Text, _textBox.SelectionStart);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        List<WordEntry> found = _services.Suggestions.GetSuggestions(
            prefix, previousWord, _services.Settings.SuggestionCount);
        clock.Stop();

        _timingLabel.Text = $"{found.Count} suggestions in {clock.Elapsed.TotalMilliseconds:0.0} ms";

        // The word being typed is kept in the list on purpose. Dropping it
        // used to empty the list the moment you finished a word, which made
        // the suggestions look like they disappeared for no reason. Leaving it
        // in also gives Tab something useful to do: finish the word and add
        // the space.
        List<string> words = found.Select(f => f.Word).ToList();

        _suggestions.Show(words, GetCaretLocation(), this);
    }

    // Finds where to put the list: just under the caret inside the text box.
    // Time O(1).
    private Point GetCaretLocation()
    {
        Point caret = _textBox.GetPositionFromCharIndex(_textBox.SelectionStart);
        int lineHeight = _textBox.Font.Height + 4;

        return new Point(
            _textBox.Left + caret.X,
            _textBox.Top + caret.Y + lineHeight);
    }

    // Swaps the half typed word for the highlighted suggestion.
    // Time O(L) where L is the length of the text being replaced.
    private void AcceptSelectedSuggestion()
    {
        string? word = _suggestions.SelectedWord;
        if (word is null)
        {
            return;
        }

        int caret = _textBox.SelectionStart;
        int start = WordScanner.GetCurrentWordStart(_textBox.Text, caret);
        string typed = _textBox.Text.Substring(start, caret - start);

        string? previousWord = WordScanner.GetPreviousWord(_textBox.Text, start);

        _suggestions.HideList();
        ReplaceRange(start, caret - start, WordScanner.MatchCapitalization(typed, word) + " ");

        // Accepting a suggestion is a clear signal about what you meant.
        _services.Learning.RecordWord(word, previousWord);
    }

    // Builds the right click menu for whatever word the caret is sitting on.
    // Time is the same as FuzzyMatcher.FindCorrections.
    private void OnWordMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _wordMenu.Items.Clear();

        FuzzyMatcher? matcher = _services.Fuzzy;
        if (matcher is null)
        {
            _wordMenu.Items.Add(new ToolStripMenuItem("Spell check is still loading") { Enabled = false });
            return;
        }

        (_clickedStart, _clickedWord) = GetWordAtCaret();

        if (_clickedWord.Length == 0 || !_painter.ShouldMarkRed(matcher, _clickedWord))
        {
            _wordMenu.Items.Add(new ToolStripMenuItem("No spelling problem here") { Enabled = false });
            return;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        List<FuzzyMatch> fixes = matcher.FindCorrections(_clickedWord);
        clock.Stop();
        _timingLabel.Text = $"{fixes.Count} fixes in {clock.Elapsed.TotalMilliseconds:0.0} ms";

        if (fixes.Count == 0)
        {
            _wordMenu.Items.Add(new ToolStripMenuItem("No close word found") { Enabled = false });
        }
        else
        {
            foreach (FuzzyMatch fix in fixes)
            {
                string replacement = WordScanner.MatchCapitalization(_clickedWord, fix.Word);
                var item = new ToolStripMenuItem(replacement)
                {
                    Font = new Font(_wordMenu.Font, FontStyle.Bold)
                };

                item.Click += (_, _) => ApplyFix(replacement);
                _wordMenu.Items.Add(item);
            }
        }

        _wordMenu.Items.Add(new ToolStripSeparator());

        var ignore = new ToolStripMenuItem("Ignore");
        ignore.Click += (_, _) =>
        {
            _painter.Ignore(_clickedWord);
            _painter.Repaint();
        };

        var add = new ToolStripMenuItem("Add to Dictionary");
        add.Click += (_, _) => AddClickedWordToDictionary();

        _wordMenu.Items.Add(ignore);
        _wordMenu.Items.Add(add);
    }

    // Reads the whole word the caret is inside, not just the part before it.
    // Time O(L).
    private (int Start, string Word) GetWordAtCaret()
    {
        string text = _textBox.Text;
        int caret = Math.Clamp(_textBox.SelectionStart, 0, text.Length);

        int start = WordScanner.GetCurrentWordStart(text, caret);
        int end = caret;
        while (end < text.Length && WordScanner.IsWordChar(text[end]))
        {
            end++;
        }

        return (start, text.Substring(start, end - start));
    }

    // Puts the chosen fix in place of the misspelled word. Time O(L).
    private void ApplyFix(string replacement)
    {
        ReplaceRange(_clickedStart, _clickedWord.Length, replacement);
        _painter.Repaint();
    }

    // Adds the word to the user's own dictionary and saves it to file, so it
    // is still known the next time the program starts.
    // Time is the same as UserDictionary.Add.
    private void AddClickedWordToDictionary()
    {
        string word = _clickedWord.ToLowerInvariant();

        if (_services.Users.Add(word))
        {
            _statusLabel.Text = "Added " + word + " to your dictionary. " + BuildStatusText();
        }
        else
        {
            _statusLabel.Text = word + " could not be added. " + BuildStatusText();
        }

        _painter.Repaint();
    }

    // Replaces a stretch of text and puts the caret after it. Time O(L).
    private void ReplaceRange(int start, int length, string replacement)
    {
        _textBox.Select(start, length);
        _textBox.SelectedText = replacement;
        _textBox.SelectionStart = start + replacement.Length;
        _textBox.SelectionLength = 0;
        _textBox.Focus();
    }

    // Runs the moment a word is finished, which is when a space or a piece of
    // punctuation is typed. The character has not been added to the text box
    // yet, so the word behind the caret is the one that was just completed.
    // Time is the same as AutocorrectEngine.Check.
    private void OnTextBoxKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!WordScanner.IsWordSeparator(e.KeyChar))
        {
            return;
        }

        RunAutocorrectOnFinishedWord();
    }

    // Checks the word sitting just behind the caret and fixes it if the engine
    // is confident enough. Time is the same as AutocorrectEngine.Check.
    private void RunAutocorrectOnFinishedWord()
    {
        AutocorrectEngine? autocorrect = _services.Autocorrect;
        if (autocorrect is null)
        {
            return;
        }

        int caret = _textBox.SelectionStart;
        int start = WordScanner.GetCurrentWordStart(_textBox.Text, caret);
        string typed = _textBox.Text.Substring(start, caret - start);

        if (typed.Length == 0)
        {
            return;
        }

        AutocorrectResult result = autocorrect.Check(typed, IsSentenceStart(start));
        string? previousWord = WordScanner.GetPreviousWord(_textBox.Text, start);

        if (!result.Changed)
        {
            // Nothing was changed, so what the user typed is what they meant.
            _services.Learning.RecordWord(typed, previousWord);
            ForgetUndo();
            return;
        }

        _suggestions.HideList();
        ReplaceRange(start, typed.Length, result.Corrected);

        // Learn the word that was actually meant, not the typo.
        _services.Learning.RecordWord(result.Corrected, previousWord);

        _undoOriginal = result.Original;
        _undoCorrected = result.Corrected;
        _undoStart = start;

        _statusLabel.Text = $"Autocorrected {result.Original} to {result.Corrected}. Ctrl+Z puts it back.";
    }

    // Puts back the word autocorrect changed. Returns false when there is
    // nothing to undo, so the text box can handle Ctrl+Z normally.
    // Time O(L).
    private bool UndoAutocorrect()
    {
        if (_undoStart < 0 || _undoCorrected.Length == 0)
        {
            return false;
        }

        // Make sure the corrected word is still sitting where we left it.
        int end = _undoStart + _undoCorrected.Length;
        if (end > _textBox.Text.Length ||
            _textBox.Text.Substring(_undoStart, _undoCorrected.Length) != _undoCorrected)
        {
            ForgetUndo();
            return false;
        }

        ReplaceRange(_undoStart, _undoCorrected.Length, _undoOriginal);
        _statusLabel.Text = $"Put {_undoOriginal} back. " + BuildStatusText();

        ForgetUndo();
        return true;
    }

    /// <summary>Drops the undo memory, because only the very next Ctrl+Z counts.</summary>
    // Time O(1).
    private void ForgetUndo()
    {
        _undoStart = -1;
        _undoOriginal = string.Empty;
        _undoCorrected = string.Empty;
    }

    // True when the word starting here is the first word of a sentence.
    // A capital letter only means "name" when it is not at a sentence start.
    // Time O(number of characters skipped backwards).
    private bool IsSentenceStart(int wordStart)
    {
        string text = _textBox.Text;

        for (int i = wordStart - 1; i >= 0; i--)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return WordScanner.IsSentenceEnd(c);
        }

        // Nothing before it at all, so it is the very first word.
        return true;
    }

    // With no word being typed, offer the words most likely to come next.
    // This only happens right after a space, because that is when the user is
    // about to start a new word. A full stop clears the context, so nothing is
    // offered at the start of a fresh sentence.
    // Time is the same as NextWordPredictor.PredictNext.
    private void ShowNextWordPredictions()
    {
        if (!_services.Settings.ShowPredictions)
        {
            _suggestions.HideList();
            _timingLabel.Text = "ready";
            return;
        }

        int caret = _textBox.SelectionStart;
        string text = _textBox.Text;

        // Only just after a space, not after a comma or at the very start.
        if (caret <= 0 || caret > text.Length || !char.IsWhiteSpace(text[caret - 1]))
        {
            _suggestions.HideList();
            _timingLabel.Text = "ready";
            return;
        }

        string? previousWord = WordScanner.GetPreviousWord(text, caret);
        if (previousWord is null)
        {
            // Start of a sentence, so there is no context worth using.
            _suggestions.HideList();
            _timingLabel.Text = "new sentence";
            return;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        List<string> next = _services.Predictions.PredictNextWords(
            previousWord, _services.Settings.SuggestionCount);
        clock.Stop();

        if (next.Count == 0)
        {
            _suggestions.HideList();
            _timingLabel.Text = "ready";
            return;
        }

        string source = _services.Predictions.LastAnswerWasFallback
            ? "most common words"
            : $"after \"{previousWord}\"";

        _timingLabel.Text = $"{next.Count} next words, {source}, in {clock.Elapsed.TotalMilliseconds:0.0} ms";

        _suggestions.Show(next, GetCaretLocation(), this);
    }

    // Asks first, then forgets everything learned and puts the counts back the
    // way they were on the first run.
    private void ResetLearning()
    {
        DialogResult answer = MessageBox.Show(
            "Forget everything SmartKeyboard has learned from your typing?" +
            Environment.NewLine + Environment.NewLine +
            "Your own dictionary words are kept.",
            "Reset learning",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        _services.Learning.Reset();
        _statusLabel.Text = "Learning reset. " + BuildStatusText();
    }

    // Anything learned since the last batch is written out before closing,
    // so nothing is lost on exit.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _services.Learning.Save();
        base.OnFormClosing(e);
    }
}
