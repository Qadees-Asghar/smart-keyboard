namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Remembers whether the user is in the middle of writing something.
///
/// SmartKeyboard watches every key on the PC, so it has to decide for itself
/// whether a keystroke is part of writing or just a key being pressed. Without
/// that, a letter tapped on the desktop put a list of word suggestions on
/// screen, which is not help, it is clutter.
///
/// A session starts on the first letter typed and stays live while the typing
/// keeps coming. It dies on its own once nothing has been typed for a couple
/// of seconds, and it can be ended at once by a click, a change of window, or
/// Escape, because all three mean the user has gone somewhere else.
///
/// This also settles an awkward corner. Suggesting the next word happens at
/// the exact moment nothing is being typed, so "only show while typing" and
/// "suggest the next word after a space" pull against each other. A session
/// tells them apart: finishing a word in the middle of a sentence happens
/// inside a live session, while pressing space once on an idle screen does
/// not.
///
/// It is plain timekeeping with no Windows in it, so it lives in Core and is
/// tested properly. The time is passed in rather than read from the clock, so
/// the tests do not have to wait for real seconds to pass.
/// </summary>
public class TypingSession
{
    /// <summary>
    /// How long after the last keystroke the session is still counted as
    /// live. Long enough to think about the next word, short enough that a
    /// popup left over from earlier does not linger.
    /// </summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(2.5);

    private DateTime? _lastKey;

    /// <summary>When the last key was typed, or null when there is no session.</summary>
    public DateTime? LastKey => _lastKey;

    // Records that the user typed something. Time O(1).
    public void NoteKey(DateTime now)
    {
        _lastKey = now;
    }

    // Ends the session at once, whatever the clock says. Used when the user
    // clicks, switches window, or presses Escape. Time O(1).
    public void End()
    {
        _lastKey = null;
    }

    // True when the user has typed recently enough to still count as writing.
    // Time O(1).
    public bool IsLive(DateTime now)
    {
        return _lastKey is DateTime last && now - last < IdleTimeout;
    }
}
