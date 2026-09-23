using System.Text;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Turns a key code into the character it actually produces.
///
/// A low level hook reports key codes, not letters. The same key gives "a" or
/// "A" depending on Shift and Caps Lock, and a different letter again on a
/// French or German keyboard. Windows already knows all of that, so we ask it
/// through ToUnicodeEx rather than trying to work it out ourselves.
/// </summary>
internal static class KeyTranslator
{
    private const int VkShift = 0x10;
    private const int VkCapital = 0x14;

    // Works out the character for a key, or null when the key produces none.
    // Time O(1).
    public static char? ToCharacter(uint virtualKey, uint scanCode)
    {
        var state = new byte[256];
        if (!NativeMethods.GetKeyboardState(state))
        {
            return null;
        }

        // Inside a low level hook the state Windows hands back is one key
        // behind, so Shift and Caps Lock are filled in by asking directly.
        state[VkShift] = (byte)(IsDown(VkShift) ? 0x80 : 0);
        state[VkCapital] = (byte)(IsToggled(VkCapital) ? 0x01 : 0);

        var buffer = new StringBuilder(8);
        IntPtr layout = NativeMethods.GetKeyboardLayout(0);

        int result = NativeMethods.ToUnicodeEx(virtualKey, scanCode, state, buffer, buffer.Capacity, 0, layout);

        // A negative result means a dead key, the kind that waits for the next
        // key to make an accented letter. Calling it again clears the state it
        // left behind, so the next real key is not spoiled.
        if (result < 0)
        {
            NativeMethods.ToUnicodeEx(virtualKey, scanCode, state, buffer, buffer.Capacity, 0, layout);
            return null;
        }

        if (result < 1 || buffer.Length == 0)
        {
            return null;
        }

        char c = buffer[0];

        // Control characters are keys, not text.
        return char.IsControl(c) ? null : c;
    }

    // True when the key is held down right now. Time O(1).
    private static bool IsDown(int virtualKey)
    {
        return (NativeMethods.GetKeyState(virtualKey) & 0x8000) != 0;
    }

    // True when a toggle key such as Caps Lock is switched on. Time O(1).
    private static bool IsToggled(int virtualKey)
    {
        return (NativeMethods.GetKeyState(virtualKey) & 0x0001) != 0;
    }
}
