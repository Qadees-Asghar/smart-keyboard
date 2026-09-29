namespace SmartKeyboard.Core.Engine;

/// <summary>A rectangle on screen, in pixels. Width can be zero for a caret.</summary>
public readonly record struct ScreenBox(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

/// <summary>
/// Works out where the caret is from the rectangles an app hands back through
/// accessibility, for apps that do not report a caret to Windows the old way.
///
/// Browsers, Electron apps and the Windows 11 Notepad draw their own caret, so
/// Windows cannot say where it is, and the popup used to sit where the user
/// last clicked instead of where they were typing. Asked through UI
/// Automation, most of them will give the rectangle of a character instead.
/// The caret is the right edge of the character just before it, or the left
/// edge of the one just after it.
///
/// The asking is Windows code in the app; the judging is here, so it can be
/// tested. Every answer from another app is checked, because some hand back
/// zeroes or a box nowhere near the text, and a popup there is worse than
/// the old guess.
/// </summary>
public static class CaretBox
{
    /// <summary>Taller than any line of text; a box this tall is a whole field, not a caret.</summary>
    public const double MaxLineHeight = 400;

    /// <summary>How far outside the text field a caret may be and still be believed.</summary>
    public const double FieldMargin = 40;

    // The caret itself, from the rectangles of an empty range at the caret.
    // Some apps, Word and the classic edit controls, answer this directly.
    // Time O(r) over the rectangles.
    public static ScreenBox? FromCaret(IReadOnlyList<ScreenBox>? boxes)
    {
        if (boxes is null || boxes.Count == 0)
        {
            return null;
        }

        ScreenBox box = boxes[boxes.Count - 1];
        return new ScreenBox(box.Left, box.Top, 0, box.Height);
    }

    // The caret from the character just before it: its right edge. A line
    // break is refused, because its box sits at the end of the line above,
    // which is exactly the wrong place for a caret at the start of a line.
    // Time O(r).
    public static ScreenBox? FromCharBefore(IReadOnlyList<ScreenBox>? boxes, string? character)
    {
        if (boxes is null || boxes.Count == 0 || IsLineBreak(character))
        {
            return null;
        }

        ScreenBox box = boxes[boxes.Count - 1];
        return new ScreenBox(box.Right, box.Top, 0, box.Height);
    }

    // The caret from the character just after it: its left edge. A line
    // break is fine here, since it starts where the caret is.
    // Time O(r).
    public static ScreenBox? FromCharAfter(IReadOnlyList<ScreenBox>? boxes)
    {
        if (boxes is null || boxes.Count == 0)
        {
            return null;
        }

        ScreenBox box = boxes[0];
        return new ScreenBox(box.Left, box.Top, 0, box.Height);
    }

    // True when a caret box is believable: a real line height, not the
    // all-zero answer of an app that does not know, and inside the field the
    // text is in. An empty field box means the app did not say, and then only
    // the other checks apply.
    // Time O(1).
    public static bool IsUsable(ScreenBox? caret, ScreenBox field)
    {
        if (caret is not ScreenBox box)
        {
            return false;
        }

        if (!double.IsFinite(box.Left) || !double.IsFinite(box.Top) || !double.IsFinite(box.Height))
        {
            return false;
        }

        if (box.Height <= 0 || box.Height > MaxLineHeight)
        {
            return false;
        }

        if (box.Left == 0 && box.Top == 0)
        {
            return false;
        }

        if (field.Width <= 0 || field.Height <= 0)
        {
            return true;
        }

        return box.Left >= field.Left - FieldMargin
            && box.Left <= field.Right + FieldMargin
            && box.Top >= field.Top - FieldMargin
            && box.Bottom <= field.Bottom + FieldMargin;
    }

    // Time O(1).
    private static bool IsLineBreak(string? character)
    {
        return !string.IsNullOrEmpty(character) && (character.Contains('\n') || character.Contains('\r'));
    }
}
