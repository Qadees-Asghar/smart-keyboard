using System.Runtime.InteropServices;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Watches every key pressed anywhere on the PC, using a low level keyboard
/// hook (SetWindowsHookEx with WH_KEYBOARD_LL).
///
/// The callback runs inside the operating system's input path, ahead of the
/// app the key is going to. If it is slow, every app on the PC feels slow.
/// So it does almost nothing: work out the character, raise an event, and get
/// out. All the real work happens elsewhere, off this thread.
///
/// Windows quietly removes a hook that takes too long, so a slow callback
/// does not only feel bad, it stops the feature working at all.
///
/// Known limit: a hook cannot see apps running as Administrator unless
/// SmartKeyboard is also running as Administrator.
/// </summary>
public class KeyboardHook : IDisposable
{
    private readonly NativeMethods.HookProc _callback;
    private IntPtr _hook = IntPtr.Zero;

    public KeyboardHook()
    {
        // Kept in a field on purpose. If this delegate were made inline, the
        // garbage collector could free it while Windows still held a pointer
        // to it, and the program would crash at a random moment later.
        _callback = OnKey;
    }

    /// <summary>Raised for a key that produced a character.</summary>
    public event EventHandler<TypedKeyEventArgs>? KeyTyped;

    /// <summary>Raised for a key that means "the caret moved", like an arrow.</summary>
    public event EventHandler<ControlKeyEventArgs>? ControlKeyPressed;

    /// <summary>True while the hook is installed.</summary>
    public bool IsRunning => _hook != IntPtr.Zero;

    /// <summary>
    /// While true the hook stays installed but does nothing. Used for Pause,
    /// and for the privacy guard when a password box has focus.
    /// </summary>
    public bool IsPaused { get; set; }

    // Installs the hook. Safe to call twice. Time O(1).
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        IntPtr module = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback, module, 0);

        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "SmartKeyboard could not watch the keyboard. Error code: " + Marshal.GetLastWin32Error());
        }
    }

    // Removes the hook. Time O(1).
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    // The hook callback. This has to stay fast. Time O(1).
    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        // A negative code means Windows says "pass this on without looking".
        if (code < 0 || IsPaused)
        {
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        int message = (int)wParam;
        if (message != NativeMethods.WM_KEYDOWN && message != NativeMethods.WM_SYSKEYDOWN)
        {
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        try
        {
            var info = Marshal.PtrToStructure<NativeMethods.KeyboardHookStruct>(lParam);

            // Keys SmartKeyboard sent itself. Reading our own output back
            // would corrupt the word being tracked, so pass them straight on.
            if (info.ExtraInfo == NativeMethods.InjectedSignature)
            {
                return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
            }

            if (Handle((Keys)info.VirtualKeyCode, info.ScanCode))
            {
                // Returning 1 swallows the key, so the app underneath never
                // sees it. This is what stops Enter both accepting a word and
                // sending the chat message it was typed into.
                return new IntPtr(1);
            }
        }
        catch (Exception)
        {
            // Never let a problem here break typing for the whole PC.
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    // Decides what kind of key this was and tells whoever is listening.
    // Returns true when the key should be swallowed instead of passed on.
    // Time O(1).
    private bool Handle(Keys key, uint scanCode)
    {
        bool ctrl = IsDown(Keys.ControlKey);
        bool alt = IsDown(Keys.Menu);

        switch (key)
        {
            case Keys.Up:
                return Raise(ControlKeyKind.MoveUp, key);

            case Keys.Down:
                return Raise(ControlKeyKind.MoveDown, key);

            case Keys.Escape:
                return Raise(ControlKeyKind.Dismiss, key);

            case Keys.Enter:
                return Raise(ControlKeyKind.Accept, key);

            // Tab is deliberately left alone. In other apps it moves to the
            // next field or the next control, and taking that away to insert
            // a word would be a nasty surprise. Enter is the accept key.
            case Keys.Tab:
            case Keys.Left:
            case Keys.Right:
            case Keys.Home:
            case Keys.End:
            case Keys.PageUp:
            case Keys.PageDown:
            case Keys.Delete:
                return Raise(ControlKeyKind.CaretMoved, key);

            case Keys.Back:
                return Raise(ControlKeyKind.Backspace, key);
        }

        // Ctrl or Alt held means a shortcut, not typing. Shift is fine.
        // The word is dropped because a shortcut usually moves the caret or
        // changes the text in a way we cannot follow.
        if (ctrl || alt)
        {
            return Raise(ControlKeyKind.CaretMoved, key);
        }

        char? typed = KeyTranslator.ToCharacter((uint)key, scanCode);
        if (typed.HasValue)
        {
            KeyTyped?.Invoke(this, new TypedKeyEventArgs(typed.Value, key));
        }

        // Letters are never swallowed. The user must always see what they type.
        return false;
    }

    // Tells the listener, and reports back whether it wants the key swallowed.
    // Time O(1).
    private bool Raise(ControlKeyKind kind, Keys key)
    {
        var args = new ControlKeyEventArgs(kind, key);
        ControlKeyPressed?.Invoke(this, args);
        return args.Handled;
    }

    // True when a key is being held down right now. Time O(1).
    private static bool IsDown(Keys key)
    {
        return (NativeMethods.GetKeyState((int)key) & 0x8000) != 0;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}

/// <summary>A key that produced a character.</summary>
public class TypedKeyEventArgs : EventArgs
{
    public TypedKeyEventArgs(char character, Keys key)
    {
        Character = character;
        Key = key;
    }

    public char Character { get; }

    public Keys Key { get; }
}

/// <summary>What a non character key meant.</summary>
public enum ControlKeyKind
{
    /// <summary>The caret moved, so we no longer know where we are.</summary>
    CaretMoved,

    /// <summary>A letter was deleted.</summary>
    Backspace,

    /// <summary>The user wants the highlighted suggestion.</summary>
    Accept,

    /// <summary>Move the highlight up the list.</summary>
    MoveUp,

    /// <summary>Move the highlight down the list.</summary>
    MoveDown,

    /// <summary>Close the popup and leave the typing alone.</summary>
    Dismiss,
}

/// <summary>A key that did not produce a character.</summary>
public class ControlKeyEventArgs : EventArgs
{
    public ControlKeyEventArgs(ControlKeyKind kind, Keys key)
    {
        Kind = kind;
        Key = key;
    }

    public ControlKeyKind Kind { get; }

    public Keys Key { get; }

    /// <summary>Set to true to stop the key reaching the app underneath.</summary>
    public bool Handled { get; set; }
}
