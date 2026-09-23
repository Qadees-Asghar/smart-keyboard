using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.App.Editor;

/// <summary>
/// Colours unknown words red inside the text box.
///
/// Repainting the whole document on every keystroke would feel slow, so the
/// work is delayed by a short pause. While you are still typing the timer keeps
/// restarting, and the repaint only happens once you stop.
///
/// One trap to know about. Setting SelectionColor on a RichTextBox raises
/// TextChanged, even though not a single character changed. Without a guard
/// the repaint would look like typing: it would hide the suggestion list,
/// and it would restart its own timer forever. IsRepainting says "this is me,
/// ignore it", and the editor checks that flag before reacting.
/// </summary>
public class SpellCheckPainter
{
    /// <summary>How long to wait after the last keystroke, in milliseconds.</summary>
    private const int PauseMs = 350;

    private readonly RichTextBox _textBox;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);

    private Func<FuzzyMatcher?> _getMatcher = () => null;

    /// <summary>When false, no word is marked red. Comes from Settings.</summary>
    public bool Enabled { get; set; } = true;

    public SpellCheckPainter(RichTextBox textBox)
    {
        _textBox = textBox;
        _timer = new System.Windows.Forms.Timer { Interval = PauseMs };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Repaint();
        };
    }

    /// <summary>Tells the painter where to get the spell checker from.</summary>
    public void UseMatcher(Func<FuzzyMatcher?> getMatcher)
    {
        _getMatcher = getMatcher;
    }

    /// <summary>Words the user chose to ignore for this session.</summary>
    public void Ignore(string word) => _ignored.Add(word);

    /// <summary>True when the word should not be marked red.</summary>
    public bool IsIgnored(string word) => _ignored.Contains(word);

    /// <summary>
    /// True while the painter is recolouring. Any TextChanged raised during
    /// that time comes from the painter itself, not from the user typing.
    /// </summary>
    public bool IsRepainting { get; private set; }

    /// <summary>Asks for a repaint soon. Call this on every keystroke.</summary>
    public void RequestRepaint()
    {
        if (IsRepainting)
        {
            return;
        }

        _timer.Stop();
        _timer.Start();
    }

    // Walks the text, finds every word, and colours the unknown ones red.
    // Time O(T) over the characters in the text, plus one Trie lookup per word.
    public void Repaint()
    {
        FuzzyMatcher? matcher = _getMatcher();
        if (matcher is null)
        {
            return;
        }

        if (IsRepainting)
        {
            return;
        }

        // Remember where the caret and the scroll position are, so the user
        // does not see the view jump while we recolour.
        int caret = _textBox.SelectionStart;
        int selectionLength = _textBox.SelectionLength;

        IsRepainting = true;
        NativeScroll.Freeze(_textBox);
        try
        {
            _textBox.SelectAll();
            _textBox.SelectionColor = _textBox.ForeColor;

            foreach ((int start, int length) in FindWords(_textBox.Text))
            {
                string word = _textBox.Text.Substring(start, length);
                if (ShouldMarkRed(matcher, word))
                {
                    _textBox.Select(start, length);
                    _textBox.SelectionColor = BrandTheme.Misspelled;
                }
            }
        }
        finally
        {
            _textBox.Select(caret, selectionLength);
            _textBox.SelectionColor = _textBox.ForeColor;
            NativeScroll.Unfreeze(_textBox);
            IsRepainting = false;
        }
    }

    // Decides whether a word gets a red mark. Time O(L).
    public bool ShouldMarkRed(FuzzyMatcher matcher, string word)
    {
        // Single letters are checked like any other word. The dictionary holds
        // only "a" and "i", so a stray "o" or "b" is marked, which is right.
        if (!Enabled || word.Length == 0 || _ignored.Contains(word))
        {
            return false;
        }

        // Anything with a digit in it is not a word we can spell check.
        if (word.Any(char.IsDigit))
        {
            return false;
        }

        return matcher.IsMisspelled(word);
    }

    // Finds the start and length of every word in the text.
    // Time O(T) over the characters in the text.
    public static IEnumerable<(int Start, int Length)> FindWords(string text)
    {
        int index = 0;
        while (index < text.Length)
        {
            if (!WordScanner.IsWordChar(text[index]))
            {
                index++;
                continue;
            }

            int start = index;
            while (index < text.Length && WordScanner.IsWordChar(text[index]))
            {
                index++;
            }

            yield return (start, index - start);
        }
    }
}
