namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Keeps the person's own keystrokes back while a word is being replaced, and
/// hands them over, in order, once it is done.
///
/// Replacing a word in another app means sending backspaces and letters, and
/// a key the person presses in the middle of that lands in the middle of it.
/// That is what turned "world" into "wworld". So while a replacement is going
/// in, keys are held here instead of reaching the app.
///
/// Three rules, each one a bug that happened when it was not kept.
///
///   1. Every hold ends by replaying what it kept. A replacement that is
///      called off still has to give the keys back. When it did not, the
///      letters sat here and were typed at the NEXT replacement, seconds
///      later and wherever the caret happened to be.
///
///   2. Holding stays on until the buffer is empty. Switching it off first and
///      replaying second left a gap, and a key pressed in that gap reached the
///      app ahead of the ones that were held, so "wo" came out as "ow".
///
///   3. Holds are counted, not switched. A suggestion accepted while a typo
///      fix is waiting starts a second hold, and when that one finished it
///      used to switch holding off for the first as well.
///
/// Safe to use from any thread. The hook thread calls TryHold, the threads
/// doing the replacing call Begin and End.
/// </summary>
public class KeyHoldGate
{
    /// <summary>
    /// The most keys held at once. A replacement is over in well under a
    /// tenth of a second, so this is never reached by real typing. It exists
    /// so that a bug could never swallow more than a few keystrokes.
    /// </summary>
    public const int MaxHeld = 8;

    /// <summary>How a held Backspace is stored, so it keeps its place in line.</summary>
    public const char Backspace = '\b';

    private readonly object _lock = new();
    private readonly System.Text.StringBuilder _held = new();
    private int _depth;

    /// <summary>True while at least one replacement is holding keys.</summary>
    public bool IsHolding
    {
        get
        {
            lock (_lock)
            {
                return _depth > 0;
            }
        }
    }

    /// <summary>How many keys are waiting to be replayed.</summary>
    public int HeldCount
    {
        get
        {
            lock (_lock)
            {
                return _held.Length;
            }
        }
    }

    // Starts holding. Every call must be matched by one call to End, in a
    // finally block. Time O(1).
    public void Begin()
    {
        lock (_lock)
        {
            _depth++;
        }
    }

    // Keeps a key back if something is holding. Returns true when the key was
    // kept, so the caller swallows it. Past the cap the key is let through
    // rather than lost, because a key in the wrong place beats one that
    // vanished. Time O(1).
    public bool TryHold(char key)
    {
        lock (_lock)
        {
            if (_depth == 0 || _held.Length >= MaxHeld)
            {
                return false;
            }

            _held.Append(key);
            return true;
        }
    }

    // Ends one hold. The last hold to end replays everything that was kept,
    // in the order it was typed, and only then stops holding.
    //
    // The replay runs outside the lock but with holding still on, so a key
    // pressed while it runs is kept too and replayed in the next round,
    // behind the ones before it. Holding stops only once a round finds
    // nothing left.
    //
    // If the replay itself starts a new hold, which happens when a replayed
    // space finishes a word that then needs fixing, that new hold takes over
    // the replaying and this one simply steps down.
    //
    // Time O(n) over the keys held, plus whatever replay costs.
    public void End(Action<string> replay)
    {
        ArgumentNullException.ThrowIfNull(replay);

        while (true)
        {
            string keys;

            lock (_lock)
            {
                if (_depth <= 0)
                {
                    // End without Begin. Nothing to do, and nothing to break.
                    _depth = 0;
                    return;
                }

                if (_depth > 1)
                {
                    // Someone else is still holding, and they will replay.
                    _depth--;
                    return;
                }

                if (_held.Length == 0)
                {
                    _depth = 0;
                    return;
                }

                keys = _held.ToString();
                _held.Clear();
            }

            try
            {
                replay(keys);
            }
            catch
            {
                // Never leave the keyboard holding keys. Anything still
                // waiting is dropped rather than replayed at some later,
                // unrelated moment.
                lock (_lock)
                {
                    _depth = Math.Max(0, _depth - 1);
                    _held.Clear();
                }

                throw;
            }
        }
    }
}
