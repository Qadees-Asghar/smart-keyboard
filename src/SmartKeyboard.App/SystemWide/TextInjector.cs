namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Types into whichever app is in front, using SendInput.
///
/// To finish a word it first sends as many Backspaces as the user has already
/// typed, then sends the whole word. Sending only the missing letters would be
/// shorter, but it breaks the moment capitalisation differs ("teh" to "The"),
/// so replacing the lot is the version that always works.
///
/// Characters are sent as Unicode rather than as key codes. That way the right
/// letter arrives whatever keyboard layout the user has.
///
/// Every key sent carries NativeMethods.InjectedSignature, so our own keyboard
/// hook can tell them apart from a person typing and ignore them.
/// </summary>
public static class TextInjector
{
    /// <summary>The most characters that will ever be sent at once.</summary>
    private const int MaxLength = 100;

    // Replaces the word being typed with a different one.
    // Returns false when Windows refused the input.
    // Time O(n) where n is the letters deleted plus the letters typed.
    public static bool ReplaceWord(string typedSoFar, string replacement)
    {
        if (string.IsNullOrEmpty(replacement) || replacement.Length > MaxLength)
        {
            return false;
        }

        int toDelete = typedSoFar?.Length ?? 0;
        if (toDelete > MaxLength)
        {
            return false;
        }

        var inputs = new List<NativeMethods.Input>(((toDelete + replacement.Length) * 2) + 2);

        for (int i = 0; i < toDelete; i++)
        {
            inputs.Add(KeyDown(NativeMethods.VK_BACK));
            inputs.Add(KeyUp(NativeMethods.VK_BACK));
        }

        foreach (char c in replacement)
        {
            inputs.Add(UnicodeDown(c));
            inputs.Add(UnicodeUp(c));
        }

        return Send(inputs);
    }

    /// <summary>Types a single character, such as the space after a word.</summary>
    // Time O(1).
    public static bool TypeCharacter(char c)
    {
        return Send(new List<NativeMethods.Input> { UnicodeDown(c), UnicodeUp(c) });
    }

    // Hands the list to Windows in one go, so nothing can be typed in between.
    // Time O(n).
    private static bool Send(List<NativeMethods.Input> inputs)
    {
        if (inputs.Count == 0)
        {
            return true;
        }

        NativeMethods.Input[] array = inputs.ToArray();
        int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>();

        uint sent = NativeMethods.SendInput((uint)array.Length, array, size);

        return sent == array.Length;
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
