using System.Runtime.InteropServices;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// The Windows functions System Wide Mode needs, declared with P/Invoke.
///
/// Everything that talks directly to Windows is gathered here so the rest of
/// the code stays readable, and so it is obvious exactly how much of the
/// operating system this feature touches. Core never sees any of it.
/// </summary>
internal static class NativeMethods
{
    // Keyboard hook

    /// <summary>Low level keyboard hook. Sees keys before the app they are going to.</summary>
    public const int WH_KEYBOARD_LL = 13;

    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;

    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? name);

    // Mouse hook. Used only to notice that the caret has been moved by a
    // click, which no key press would ever tell us about.

    /// <summary>Low level mouse hook.</summary>
    public const int WH_MOUSE_LL = 14;

    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_MBUTTONDOWN = 0x0207;

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseHookStruct
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    /// <summary>Which window is under a point on screen.</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(System.Drawing.Point point);

    /// <summary>The top level window a control belongs to.</summary>
    public const int GA_ROOT = 2;

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr window, int flags);

    // Turning a key into a letter

    [DllImport("user32.dll")]
    public static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyboardState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder buffer,
        int bufferSize,
        uint flags,
        IntPtr layout);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetKeyboardState(byte[] state);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    public static extern IntPtr GetKeyboardLayout(uint threadId);

    // Finding the text cursor

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GuiThreadInfo
    {
        public int Size;
        public int Flags;
        public IntPtr Active;
        public IntPtr Focus;
        public IntPtr Capture;
        public IntPtr MenuOwner;
        public IntPtr MoveSize;
        public IntPtr Caret;
        public Rect CaretRect;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClientToScreen(IntPtr window, ref System.Drawing.Point point);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetClassName(IntPtr window, System.Text.StringBuilder text, int count);

    /// <summary>Asks a control about itself. Used to spot a password box.</summary>
    public const int EM_GETPASSWORDCHAR = 0x00D2;

    /// <summary>Give up rather than wait for a window that is busy.</summary>
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr window,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeoutMs,
        out IntPtr result);

    // Typing text into another app

    /// <summary>
    /// Stamped on every keystroke SmartKeyboard sends itself.
    ///
    /// SendInput keys travel through low level hooks exactly like real ones,
    /// including our own. Without a mark to tell them apart, typing a
    /// suggestion makes the hook read its own output back: the backspaces
    /// empty the word tracker, which resets it, and the letters build up a
    /// word that was never typed by a person. Any value will do, it only has
    /// to be ours.
    /// </summary>
    public static readonly IntPtr InjectedSignature = new(0x5B17A0D5);

    public const int INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const ushort VK_BACK = 0x08;

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    /// <summary>
    /// Only here to give the union below its correct width.
    ///
    /// SendInput can carry a mouse event, a keyboard event or a hardware
    /// event, and the union is as wide as the largest of the three, which is
    /// the mouse one at 32 bytes. SmartKeyboard never sends mouse input, but
    /// the field has to exist or the struct comes out too small.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    /// <summary>
    /// A real union: both fields start at byte 0 and share the same memory.
    ///
    /// This has to be Explicit. Written as Sequential the two members sit one
    /// after the other instead of overlapping, which made the whole INPUT
    /// struct 48 bytes when Windows expects 40. SendInput checks that size
    /// and, when it does not match, quietly sends nothing at all and returns
    /// zero. Nothing crashes and no error is reported: the keystrokes simply
    /// never happen, which is exactly what it looked like from the outside.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public int Type;
        public InputUnion Union;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint count, Input[] inputs, int size);

    // The window style that stops a window ever taking focus.

    /// <summary>Windows asks a window whether a click should activate it.</summary>
    public const int WM_MOUSEACTIVATE = 0x0021;

    /// <summary>The answer: take the click, but do not take focus.</summary>
    public const int MA_NOACTIVATE = 3;

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    // Global hotkey

    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr window, int id, int modifiers, int virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr window, int id);
}
