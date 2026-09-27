using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Types into whichever app is in front, using SendInput.
///
/// Two rules here, and the second one was learned the hard way.
///
///   1. Send as few keys as possible. ReplacementPlan keeps the letters that
///      are already correct, so finishing "wor" into "world" sends no
///      backspaces at all rather than three, and a key never sent cannot go
///      astray. Every key also carries a real scan code, because a Backspace
///      sent with a scan code of zero can be dropped by a Chromium window
///      without a trace, while Unicode letters take a different path and
///      arrive regardless.
///
///   2. Send the whole replacement in ONE call to SendInput. This is not a
///      tidiness point, it is the only thing keeping the user's own typing
///      out of the middle of ours. Windows guarantees that the events in a
///      single SendInput call are not interspersed with anything else,
///      including keys the person is pressing at that moment. Split across
///      several calls, that guarantee is gone.
///
/// An earlier version split the deletes from the letters and slept between
/// them, on the theory that a slow app needed time to keep up. It gave that
/// guarantee away, and since a correction starts about 90 ms after the space,
/// the next word was often already being typed. The user's keystrokes landed
/// in the middle of ours and came out as "wlord wworld". The diagnostics log
/// is what settled it: Windows had accepted every keystroke, 10 of 10 and
/// 22 of 22, so nothing was being refused or dropped. They were simply
/// arriving mixed together.
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

    /// <summary>What a replacement attempt did, for the optional log.</summary>
    public readonly record struct InjectionResult(bool Ok, int Deleted, int Typed, int Sent, int Expected);

    // Replaces the word being typed with a different one.
    // Returns false when Windows refused the input.
    // Time O(n), and it is one call, so nothing can get in between.
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

        var inputs = new List<NativeMethods.Input>((plan.Backspaces + plan.ToType.Length) * 2);

        for (int i = 0; i < plan.Backspaces; i++)
        {
            inputs.Add(KeyDown(NativeMethods.VK_BACK));
            inputs.Add(KeyUp(NativeMethods.VK_BACK));
        }

        foreach (char c in plan.ToType)
        {
            inputs.Add(UnicodeDown(c));
            inputs.Add(UnicodeUp(c));
        }

        int sent = Send(inputs.ToArray());

        return new InjectionResult(
            sent == inputs.Count, plan.Backspaces, plan.ToType.Length, sent, inputs.Count);
    }

    /// <summary>Types a single character, such as the space after a word.</summary>
    // Time O(1).
    public static bool TypeCharacter(char c)
    {
        return Send(new[] { UnicodeDown(c), UnicodeUp(c) }) == 2;
    }

    /// <summary>Sends one Backspace, such as one held back during a replacement.</summary>
    // Time O(1).
    public static bool TypeBackspace()
    {
        return Send(new[] { KeyDown(NativeMethods.VK_BACK), KeyUp(NativeMethods.VK_BACK) }) == 2;
    }

    // Hands a block to Windows and reports how many it accepted. Time O(n).
    private static int Send(NativeMethods.Input[] inputs)
    {
        if (inputs.Length == 0)
        {
            return 0;
        }

        int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>();

        return (int)NativeMethods.SendInput((uint)inputs.Length, inputs, size);
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
