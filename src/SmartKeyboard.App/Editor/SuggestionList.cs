namespace SmartKeyboard.App.Editor;

/// <summary>
/// The little list of suggested words that appears under the caret.
///
/// It draws its own rows, because a plain ListBox always paints the selected
/// row in the Windows blue. Drawing it here lets the brand orange be used
/// instead, and lets the chosen row show its Enter hint.
/// </summary>
public class SuggestionList : ListBox
{
    private const int RowPadding = 6;

    public SuggestionList()
    {
        BorderStyle = BorderStyle.FixedSingle;
        Font = BrandTheme.Ui(10F);
        ForeColor = BrandTheme.Dark;
        BackColor = BrandTheme.Light;
        IntegralHeight = false;
        Visible = false;
        TabStop = false;

        // Fixed height rows, drawn by OnDrawItem below.
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = Font.Height + RowPadding;
    }

    /// <summary>The word currently highlighted, or null when the list is empty.</summary>
    public string? SelectedWord => SelectedItem as string;

    // Fills the list and sizes it to fit. Time O(n) over the words shown.
    public void Show(IReadOnlyList<string> words, Point location, Control owner)
    {
        if (words.Count == 0)
        {
            HideList();
            return;
        }

        BeginUpdate();
        Items.Clear();
        foreach (string word in words)
        {
            Items.Add(word);
        }

        EndUpdate();

        SelectedIndex = 0;
        Height = (ItemHeight * words.Count) + 2;
        Width = Math.Max(160, MeasureWidest(words));
        Location = KeepOnScreen(location, owner);

        Visible = true;
        BringToFront();
    }

    /// <summary>Hides the list and forgets what was in it.</summary>
    public void HideList()
    {
        Visible = false;
        Items.Clear();
    }

    // Moves the selection up or down, wrapping around at the ends.
    // Time O(1).
    public void MoveSelection(int step)
    {
        if (Items.Count == 0)
        {
            return;
        }

        SelectedIndex = (SelectedIndex + step + Items.Count) % Items.Count;
    }

    // Paints one row. The chosen row gets the brand orange behind it, and the
    // first row shows a Tab hint so the shortcut is easy to find.
    // Time O(1) per row.
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
        {
            return;
        }

        bool chosen = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        Color background = chosen ? BrandTheme.Orange : BrandTheme.Light;
        Color foreground = chosen ? BrandTheme.Light : BrandTheme.Dark;

        using (var brush = new SolidBrush(background))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        string word = Items[e.Index]?.ToString() ?? string.Empty;
        var textArea = new Rectangle(
            e.Bounds.X + 8,
            e.Bounds.Y + (RowPadding / 2),
            e.Bounds.Width - 16,
            e.Bounds.Height);

        TextRenderer.DrawText(e.Graphics, word, Font, textArea, foreground, TextFormatFlags.Left);

        if (chosen)
        {
            TextRenderer.DrawText(
                e.Graphics,
                "Enter",
                Font,
                textArea,
                Color.FromArgb(220, BrandTheme.Light),
                TextFormatFlags.Right);
        }
    }

    // Works out how wide the list has to be, leaving room for the Tab hint.
    // Time O(n).
    private int MeasureWidest(IReadOnlyList<string> words)
    {
        int widest = 0;
        foreach (string word in words)
        {
            widest = Math.Max(widest, TextRenderer.MeasureText(word, Font).Width);
        }

        return widest + 84;
    }

    // Nudges the list back inside the window if it would hang off the edge.
    // Time O(1).
    private Point KeepOnScreen(Point wanted, Control owner)
    {
        int x = Math.Min(wanted.X, Math.Max(0, owner.ClientSize.Width - Width));
        int y = wanted.Y;

        // Not enough room below, so show it above the caret instead.
        if (y + Height > owner.ClientSize.Height)
        {
            y = Math.Max(0, wanted.Y - Height - 20);
        }

        return new Point(Math.Max(0, x), y);
    }
}
