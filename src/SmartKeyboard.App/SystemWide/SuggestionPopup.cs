namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// The small window that floats over other apps showing suggestions.
///
/// The whole trick here is that it must never take focus. If it did, the app
/// you were typing into would lose the caret and your next keystroke would go
/// to the wrong place. Three things together make that work:
///
///   WS_EX_NOACTIVATE   Windows never gives this window focus, even on a click
///   ShowWithoutActivation   stops Windows Forms activating it when shown
///   TopMost            keeps it above the app being typed into
///
/// It is drawn by hand rather than using controls, because every control is
/// one more thing that could grab focus.
/// </summary>
public class SuggestionPopup : Form
{
    private const int RowHeight = 26;
    private const int SidePadding = 10;
    private const int MaxRows = 10;

    private readonly List<string> _words = new();
    private int _selected;

    public SuggestionPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = BrandTheme.Light;
        Font = BrandTheme.Ui(9.5F);
        DoubleBuffered = true;
        Visible = false;

        // A pointing hand says the rows can be clicked.
        Cursor = Cursors.Hand;
    }

    /// <summary>Raised when a row is clicked. The number is the row.</summary>
    public event EventHandler<int>? WordClicked;

    /// <summary>Raised when the pointer moves onto a different row.</summary>
    public event EventHandler<int>? RowHovered;

    /// <summary>Tells Windows Forms not to activate this window when it appears.</summary>
    protected override bool ShowWithoutActivation => true;

    /// <summary>Adds the styles that stop the window ever taking focus.</summary>
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;

            parameters.ExStyle |= NativeMethods.WS_EX_NOACTIVATE
                                | NativeMethods.WS_EX_TOPMOST
                                | NativeMethods.WS_EX_TOOLWINDOW;

            return parameters;
        }
    }

    /// <summary>The highlighted word, or null when nothing is showing.</summary>
    public string? SelectedWord =>
        _selected >= 0 && _selected < _words.Count ? _words[_selected] : null;

    /// <summary>How many words are showing.</summary>
    public int WordCount => _words.Count;

    // Shows the words at a place on screen, without stealing focus.
    // Which row is highlighted is decided by the controller, because the
    // keyboard hook needs to know it without crossing to the UI thread.
    // Time O(n) over the words shown.
    public void ShowWords(IReadOnlyList<string> words, Point screenLocation, int selectedIndex)
    {
        if (words.Count == 0)
        {
            HidePopup();
            return;
        }

        _words.Clear();
        _words.AddRange(words.Take(MaxRows));
        _selected = Math.Clamp(selectedIndex, 0, Math.Max(0, _words.Count - 1));

        Size = new Size(MeasureWidth(), (RowHeight * _words.Count) + 2);
        Location = KeepOnScreen(screenLocation);

        if (!Visible)
        {
            Show();
        }

        Invalidate();
    }

    /// <summary>Hides the popup and forgets what was in it.</summary>
    // Time O(1).
    public void HidePopup()
    {
        _words.Clear();
        _selected = 0;

        if (Visible)
        {
            Hide();
        }
    }

    /// <summary>Highlights a row. The controller decides which. Time O(1).</summary>
    public void SetSelection(int index)
    {
        if (_words.Count == 0)
        {
            return;
        }

        _selected = Math.Clamp(index, 0, _words.Count - 1);
        Invalidate();
    }

    // Tells Windows to deliver the click but leave focus where it is.
    //
    // WS_EX_NOACTIVATE already stops this window being activated, but the
    // window underneath is asked as well, and answering here makes sure the
    // app being typed into keeps the caret. Without that the click would
    // land, the word would be typed, and it would go somewhere else.
    // Time O(1).
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            message.Result = new IntPtr(NativeMethods.MA_NOACTIVATE);
            return;
        }

        base.WndProc(ref message);
    }

    // A click on a row means "use this word". Time O(1).
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        int row = RowAt(e.Y);
        if (row >= 0)
        {
            WordClicked?.Invoke(this, row);
        }
    }

    // Highlights whichever row the pointer is over, so it is obvious that the
    // list can be clicked and which word would be picked. Time O(1).
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int row = RowAt(e.Y);
        if (row >= 0 && row != _selected)
        {
            _selected = row;
            Invalidate();

            // The controller keeps its own copy of which row is chosen,
            // because the keyboard hook reads it. Keep the two in step, or
            // Enter would accept a different word from the highlighted one.
            RowHovered?.Invoke(this, row);
        }
    }

    // Which row a point falls on, or -1 when it is past the last one.
    // Time O(1).
    private int RowAt(int y)
    {
        int row = y / RowHeight;
        return row >= 0 && row < _words.Count ? row : -1;
    }

    // Draws the whole popup by hand. Time O(n) over the rows.
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.Clear(BrandTheme.Light);

        for (int i = 0; i < _words.Count; i++)
        {
            var row = new Rectangle(0, i * RowHeight, Width, RowHeight);
            bool chosen = i == _selected;

            if (chosen)
            {
                using var highlight = new SolidBrush(BrandTheme.Orange);
                graphics.FillRectangle(highlight, row);
            }

            Color textColour = chosen ? BrandTheme.Light : BrandTheme.Dark;
            var textArea = new Rectangle(row.X + SidePadding, row.Y, row.Width - (SidePadding * 2), row.Height);

            TextRenderer.DrawText(
                graphics,
                _words[i],
                Font,
                textArea,
                textColour,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // Only the chosen row carries a hint. Enter is the one and only
            // way to accept, so there is nothing else worth saying here.
            if (chosen)
            {
                TextRenderer.DrawText(
                    graphics,
                    "Enter",
                    Font,
                    textArea,
                    BrandTheme.Light,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        using var border = new Pen(BrandTheme.MidGray);
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    // Works out how wide the popup needs to be. Time O(n).
    private int MeasureWidth()
    {
        int widest = 0;
        foreach (string word in _words)
        {
            widest = Math.Max(widest, TextRenderer.MeasureText(word, Font).Width);
        }

        return Math.Max(150, widest + 76);
    }

    // Keeps the popup fully on whichever screen it is nearest to, and flips it
    // above the caret when there is no room below. Time O(1).
    private Point KeepOnScreen(Point wanted)
    {
        Rectangle screen = Screen.FromPoint(wanted).WorkingArea;

        int x = Math.Min(wanted.X, screen.Right - Width);
        x = Math.Max(x, screen.Left);

        int y = wanted.Y;
        if (y + Height > screen.Bottom)
        {
            // Above the caret instead. The 24 leaves room for the line itself.
            y = wanted.Y - Height - 24;
        }

        y = Math.Clamp(y, screen.Top, Math.Max(screen.Top, screen.Bottom - Height));

        return new Point(x, y);
    }
}
