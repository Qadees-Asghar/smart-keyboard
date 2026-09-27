using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Reads the few characters just before the caret in whichever app has
/// focus, through Windows UI Automation.
///
/// SmartKeyboard follows typing from keystrokes alone, so after a click or a
/// change of window it cannot tell whether the next letters start a new word
/// or finish one already on screen. That first word used to be skipped. Most
/// apps will say what is before the caret when asked through accessibility:
/// Notepad, Word, WordPad, and Chrome, Edge and Electron apps once their
/// accessibility is awake. CaretTextCheck then decides whether what came back
/// proves the word is whole.
///
/// Every call is a question to another program, so it is never made on the
/// keyboard hook thread, it gives up after a short wait, and any failure at
/// all is reported as null, meaning "cannot tell". Cannot tell leaves things
/// exactly as they were before this existed: the word is simply skipped.
/// </summary>
public static class CaretTextReader
{
    /// <summary>
    /// How long to wait for the other app to answer. An app that is busy, or
    /// Chrome waking its accessibility for the first time, can take far
    /// longer than this, and then the answer is not worth waiting for.
    /// </summary>
    public const int TimeoutMs = 150;

    // Returns up to count characters just before the caret, fewer at the
    // start of the text, or null when the app would not say or took too
    // long. A selection instead of a plain caret also gives null, because
    // then typing replaces the selection and "before the caret" is not the
    // right question.
    // Time: one short round trip to the other app, capped at TimeoutMs.
    public static string? ReadBeforeCaret(int count)
    {
        if (count <= 0)
        {
            return null;
        }

        try
        {
            Task<string?> reading = Task.Run(() => Read(count));

            // The reading carries on in the background if it runs over, and
            // its answer is simply dropped.
            return reading.Wait(TimeoutMs) ? reading.Result : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // The actual question. Time: one round trip to the other app.
    private static string? Read(int count)
    {
        try
        {
            AutomationElement? focused = AutomationElement.FocusedElement;
            if (focused is null)
            {
                return null;
            }

            if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out object pattern))
            {
                return null;
            }

            TextPatternRange[] selection = ((TextPattern)pattern).GetSelection();
            if (selection.Length != 1)
            {
                return null;
            }

            TextPatternRange caret = selection[0];

            // A real selection, not just a caret.
            if (caret.CompareEndpoints(TextPatternRangeEndpoint.Start, caret, TextPatternRangeEndpoint.End) != 0)
            {
                return null;
            }

            TextPatternRange before = caret.Clone();
            before.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -count);

            string text = before.GetText(count + 1) ?? string.Empty;

            // Some apps count a line break as one character and hand back two.
            // Only the last count characters were asked about.
            return text.Length > count ? text[^count..] : text;
        }
        catch (Exception)
        {
            // The app closed, lost focus, or does not support this fully.
            return null;
        }
    }
}
