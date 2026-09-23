using System.Runtime.InteropServices;

namespace SmartKeyboard.App.Editor;

/// <summary>
/// Stops a control from redrawing while we make lots of small changes to it.
/// Without this, recolouring every word makes the text box flicker.
///
/// This is the only place in Editor Mode that talks to Windows directly.
/// Core never does.
/// </summary>
internal static class NativeScroll
{
    private const int WM_SETREDRAW = 0x000B;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    /// <summary>Turns drawing off.</summary>
    public static void Freeze(Control control)
    {
        SendMessage(control.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Turns drawing back on and redraws once.</summary>
    public static void Unfreeze(Control control)
    {
        SendMessage(control.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
        control.Refresh();
    }
}
