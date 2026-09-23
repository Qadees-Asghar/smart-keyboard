using System.Runtime.InteropServices;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Watches for mouse clicks anywhere on the PC, using a low level mouse hook.
///
/// This exists for one reason. SmartKeyboard follows the word you are typing
/// by counting keys, because it cannot read the other app's text. The moment
/// you click somewhere else, the caret moves and that count is wrong, but no
/// key was pressed, so nothing tells it. It carries on believing you are half
/// way through a word that is no longer in front of the caret, and accepting a
/// suggestion then deletes letters that belong to something else.
///
/// Only button presses matter. Mouse movement fires constantly, so it is
/// thrown away in the first line rather than being looked at, because this
/// callback sits in the input path of every mouse event on the PC.
/// </summary>
public class MouseHook : IDisposable
{
    private readonly NativeMethods.HookProc _callback;
    private IntPtr _hook = IntPtr.Zero;

    public MouseHook()
    {
        // Kept in a field for the same reason as in KeyboardHook: the garbage
        // collector must not free it while Windows still holds a pointer.
        _callback = OnMouse;
    }

    /// <summary>Raised when a mouse button goes down, with the screen point.</summary>
    public event EventHandler<Point>? Clicked;

    /// <summary>True while the hook is installed.</summary>
    public bool IsRunning => _hook != IntPtr.Zero;

    /// <summary>While true the hook stays installed but does nothing.</summary>
    public bool IsPaused { get; set; }

    // Installs the hook. Safe to call twice. Time O(1).
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        IntPtr module = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _callback, module, 0);

        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "SmartKeyboard could not watch the mouse. Error code: " + Marshal.GetLastWin32Error());
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

    // The hook callback. Every mouse move on the PC comes through here, so
    // the common case has to cost almost nothing. Time O(1).
    private IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0 || IsPaused || !IsButtonDown((int)wParam))
        {
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        try
        {
            var info = Marshal.PtrToStructure<NativeMethods.MouseHookStruct>(lParam);
            Clicked?.Invoke(this, new Point(info.X, info.Y));
        }
        catch (Exception)
        {
            // Never let a problem here break the mouse for the whole PC.
        }

        // Clicks are always passed on. Swallowing one would be unforgivable.
        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    // True for a press of any of the three buttons. Time O(1).
    private static bool IsButtonDown(int message)
    {
        return message == NativeMethods.WM_LBUTTONDOWN
            || message == NativeMethods.WM_RBUTTONDOWN
            || message == NativeMethods.WM_MBUTTONDOWN;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
