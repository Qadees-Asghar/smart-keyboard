using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Types into whichever app is in front, using SendInput.
///
/// Two rules here, and both were learned the hard way.
///
///   1. Send as few keys as possible. ReplacementPlan keeps the letters that
///      are already correct, so finishing "wor" into "world" sends no
///      backspaces at all rather than three, and a key never sent cannot go
///      astray. Every key also carries a real scan code, because a Backspace
///      sent with a scan code of zero can be dropped by a Chromium window
///      without a trace, while Unicode letters take a different path and
///      arrive regardless.
///
///   2. Leave KeyGapMs between key presses. The Store version of Notepad
///      hands keys to another thread, and a burst sent with no gap comes out
///      scrambled: "email form the " with the "form the " part replaced in
///      one burst gave "email forrom     " and worse, every time, with
///      Windows reporting all 32 events accepted. Measured on that Notepad:
///      no gap failed 6 of 6, 5 ms failed 2 of 5, 15 ms came out right 8 of 8.
///
/// This used to be the opposite rule: everything in ONE SendInput call,
/// because Windows guarantees nothing else lands in the middle of a single
/// call, and the user's own keystrokes were landing in the middle of ours
/// ("wlord wworld"). That job now belongs to KeyHoldGate. Every caller holds
/// the person's keys back for the whole replacement and replays them after,
/// so the keys can be spaced out without anything getting between them.
///
/// Characters are sent as Unicode rather than as key codes, so the right
/// letter arrives whatever keyboard layout the user has.
///
/// Every key carries NativeMethods.InjectedSignature, so our own hook can
/// tell them apart from a person typing and ignore them.
/// </summary>
public static class TextInjector
{
    /// <summary>The most characters that will ever be sent at once.</summary>
    private const int MaxLength = 100;

    /// <summary>
    /// The least time between two key presses we send, in milliseconds.
    /// See rule 2 above for how it was measured. A typo fix of five keys
    /// takes about 75 ms at this pace, and a mixup fix of sixteen about 240.
    /// </summary>
    public const int KeyGapMs = 15;

    // When the last key press went out, so the gap holds across calls too:
    // held keys are replayed one call at a time straight after a replacement.
    private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    private static readonly object SendLock = new();
    private static long _lastSentMs = -KeyGapMs;

    /// <summary>What a replacement attempt did, for the optional log.</summary>
    public readonly record struct InjectionResult(bool Ok, int Deleted, int Typed, int Sent, int Expected);

    // Replaces the word being typed with a different one.
    // Returns false when Windows refused the input.
    // Time O(n), plus KeyGapMs per key. Callers hold the person's keys.
    public static bool ReplaceWord(string typedSoFar, string replacement)
    {
        return Replace(typedSoFar, replacement).Ok;
    }

    // The same, but says how much was sent, so the diagnostics log can tell a
    // refusal by Windows apart from an app quietly dropping the keys. That
    // distinction is what found the last bug.
    // Time as above.
    public static InjectionResult Replace(string typedSoFar, string replacement)
    {
        string from = typedSoFar ?? string.Empty;

        if (string.IsNullOrEmpty(replacement) || replacement.Length > MaxLength || from.Length > MaxLength)
        {
            return new InjectionResult(false, 0, 0, 0, 0);
        }

        // Only the tail that actually differs is touched.
        ReplacementPlan plan = ReplacementPlan.Compute(from, replacement);

        if (plan.IsNothingToDo)
        {
            return new InjectionResult(true, 0, 0, 0, 0);
        }

        int expected = (plan.Backspaces + plan.ToType.Length) * 2;
        int sent = 0;

        // One key press at a time, each with its gap. Stops at the first key
        // Windows refuses, because typing on after a lost key would put the
        // rest in the wrong place.
        for (int i = 0; i < plan.Backspaces; i++)
        {
            int accepted = Send(KeyDown(NativeMethods.VK_BACK), KeyUp(NativeMethods.VK_BACK));
            sent += accepted;

            if (accepted != 2)
            {
                return new InjectionResult(false, plan.Backspaces, plan.ToType.Length, sent, expected);
            }
        }

        foreach (char c in plan.ToType)
        {
            int accepted = Send(UnicodeDown(c), UnicodeUp(c));
            sent += accepted;

            if (accepted != 2)
            {
                return new InjectionResult(false, plan.Backspaces, plan.ToType.Length, sent, expected);
            }
        }

        return new InjectionResult(true, plan.Backspaces, plan.ToType.Length, sent, expected);
    }

    /// <summary>Types a single character, such as the space after a word.</summary>
    // Time O(1).
    public static bool TypeCharacter(char c)
    {
        return Send(UnicodeDown(c), UnicodeUp(c)) == 2;
    }

    /// <summary>Sends one Backspace, such as one held back during a replacement.</summary>
    // Time O(1).
    public static bool TypeBackspace()
    {
        return Send(KeyDown(NativeMethods.VK_BACK), KeyUp(NativeMethods.VK_BACK)) == 2;
    }

    // Sends one key press, its down and its up together, no sooner than
    // KeyGapMs after the last one, and reports how many events Windows took.
    //
    // The wait is measured on a clock rather than trusted to Thread.Sleep,
    // whose length depends on the system timer: 15 ms on a quiet PC, 1 ms
    // when a browser has asked for finer timing. Sleeping a millisecond at a
    // time keeps it off the CPU.
    // Time O(1), plus the wait.
    private static int Send(NativeMethods.Input down, NativeMethods.Input up)
    {
        lock (SendLock)
        {
            long due = _lastSentMs + KeyGapMs;
            while (Clock.ElapsedMilliseconds < due)
            {
                Thread.Sleep(1);
            }

            int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>();
            int accepted = (int)NativeMethods.SendInput(2, new[] { down, up }, size);

            _lastSentMs = Clock.ElapsedMilliseconds;
            return accepted;
        }
    }

    // The scan code Windows uses for this key on the current layout. Sending
    // zero here is what let Chromium drop our backspaces. Time O(1).
    private static ushort ScanCodeFor(ushort virtualKey)
    {
        return (ushort)NativeMethods.MapVirtualKey(virtualKey, NativeMethods.MAPVK_VK_TO_VSC);
    }

    // Time O(1).
    private static NativeMethods.Input KeyDown(ushort virtualKey) => new()
    {
        Type = NativeMethods.INPUT_KEYBOARD,
        Union = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = ScanCodeFor(virtualKey),
                ExtraInfo = NativeMethods.InjectedSignature,
            },
        },
    };

    // Time O(1).
    private static NativeMethods.Input KeyUp(ushort virtualKey) => new()
    {
        Type = NativeMethods.INPUT_KEYBOARD,
        Union = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = ScanCodeFor(virtualKey),
                Flags = NativeMethods.KEYEVENTF_KEYUP,
                ExtraInfo = NativeMethods.InjectedSignature,
            },
        },
    };

    // Sends a character by its Unicode value, not by which key makes it.
    // Time O(1).
    private static NativeMethods.Input UnicodeDown(char c) => new()
    {
        Type = NativeMethods.INPUT_KEYBOARD,
        Union = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                ScanCode = c,
                Flags = NativeMethods.KEYEVENTF_UNICODE,
                ExtraInfo = NativeMethods.InjectedSignature,
            },
        },
    };

    // Time O(1).
    private static NativeMethods.Input UnicodeUp(char c) => new()
    {
        Type = NativeMethods.INPUT_KEYBOARD,
        Union = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                ScanCode = c,
                Flags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                ExtraInfo = NativeMethods.InjectedSignature,
            },
        },
    };
}
