using System.Text;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Finds where the text cursor is on screen, so the popup can appear under it.
///
/// Windows will tell you through GetGUIThreadInfo, but only if the app bothers
/// to report a caret. Plain Windows controls do, which covers Word and most
/// older desktop programs. Browsers, Electron apps and the Windows 11 Notepad
/// draw their own caret and report nothing, so for those the app is asked
/// through UI Automation where the characters around the caret are.
///
/// Both of those are questions to another program, so Find is never called
/// on the UI or keyboard hook thread. And it is written to fail politely:
/// when the caret cannot be found, it says so, and the caller falls back to
/// where the user last clicked. That is not perfect, but it is far better
/// than a popup in the corner of the screen.
/// </summary>
public static class CaretLocator
{
    /// <summary>How far below the caret the popup sits.</summary>
    private const int DropBelowCaret = 4;

    /// <summary>Used when the app reports a caret with no height.</summary>
    private const int AssumedLineHeight = 18;

    /// <summary>Where to put the popup, and whether it is a real caret position.</summary>
    public readonly struct CaretPosition
    {
        public CaretPosition(Point location, bool isRealCaret)
        {
            Location = location;
            IsRealCaret = isRealCaret;
        }

        /// <summary>Screen position for the top left of the popup.</summary>
        public Point Location { get; }

        /// <summary>False when this is the mouse position, not the caret.</summary>
        public bool IsRealCaret { get; }
    }

    // Finds the caret in whichever app is in front.
    // Time O(1), a handful of Windows calls, plus at most one UI Automation
    // question capped at CaretTextReader.TimeoutMs.
    public static CaretPosition Find()
    {
        try
        {
            if (TryGetCaret(out Point caret))
            {
                return new CaretPosition(caret, isRealCaret: true);
            }

            if (CaretTextReader.ReadCaretBox() is SmartKeyboard.Core.Engine.ScreenBox box)
            {
                var below = new Point(
                    (int)Math.Round(box.Left),
                    (int)Math.Round(box.Bottom) + DropBelowCaret);

                return new CaretPosition(below, isRealCaret: true);
            }
        }
        catch (Exception)
        {
            // Any trouble here just means we fall back to the mouse.
        }

        return new CaretPosition(MouseFallback(), isRealCaret: false);
    }

    /// <summary>The title of the window in front. Empty when it cannot be read.</summary>
    // Time O(1).
    public static string GetForegroundWindowTitle()
    {
        IntPtr window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return string.Empty;
        }

        var text = new StringBuilder(512);
        int length = NativeMethods.GetWindowText(window, text, text.Capacity);

        return length > 0 ? text.ToString() : string.Empty;
    }

    /// <summary>The control with focus in the app in front, or zero.</summary>
    // Time O(1).
    public static IntPtr GetFocusedControl()
    {
        IntPtr window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        uint threadId = NativeMethods.GetWindowThreadProcessId(window, out _);

        var info = new NativeMethods.GuiThreadInfo();
        info.Size = System.Runtime.InteropServices.Marshal.SizeOf(info);

        return NativeMethods.GetGUIThreadInfo(threadId, ref info) ? info.Focus : IntPtr.Zero;
    }

    /// <summary>What the focused thing looks like, for deciding if it takes text.</summary>
    public readonly struct TextTarget
    {
        public TextTarget(bool hasCaret, bool hasFocus, string controlClass)
        {
            HasCaret = hasCaret;
            HasFocus = hasFocus;
            ControlClass = controlClass;
        }

        /// <summary>True when the app reports a real caret.</summary>
        public bool HasCaret { get; }

        /// <summary>True when anything at all has keyboard focus.</summary>
        public bool HasFocus { get; }

        /// <summary>The window class of the focused control, in lower case.</summary>
        public string ControlClass { get; }
    }

    // Gathers the three signals that say whether typing would go anywhere,
    // in one pass, because each of them costs a call into another program.
    // The judging itself is in Core, where it can be tested.
    // Time O(1), a handful of Windows calls.
    public static TextTarget DescribeTarget()
    {
        try
        {
            IntPtr window = NativeMethods.GetForegroundWindow();
            if (window == IntPtr.Zero)
            {
                return new TextTarget(false, false, string.Empty);
            }

            uint threadId = NativeMethods.GetWindowThreadProcessId(window, out _);

            var info = new NativeMethods.GuiThreadInfo();
            info.Size = System.Runtime.InteropServices.Marshal.SizeOf(info);

            if (!NativeMethods.GetGUIThreadInfo(threadId, ref info) || info.Focus == IntPtr.Zero)
            {
                return new TextTarget(false, false, string.Empty);
            }

            NativeMethods.Rect box = info.CaretRect;
            bool hasCaret = box.Left != 0 || box.Top != 0 || box.Right != 0 || box.Bottom != 0;

            return new TextTarget(hasCaret, true, ReadClassName(info.Focus));
        }
        catch (Exception)
        {
            // Cannot tell. Say nothing has focus rather than guess wrongly.
            return new TextTarget(false, false, string.Empty);
        }
    }

    // The window class of a control, in lower case. Time O(1).
    private static string ReadClassName(IntPtr control)
    {
        var name = new StringBuilder(256);

        return NativeMethods.GetClassName(control, name, name.Capacity) == 0
            ? string.Empty
            : name.ToString().ToLowerInvariant();
    }

    // Asks Windows where the caret is, in screen coordinates.
    // Time O(1).
    private static bool TryGetCaret(out Point location)
    {
        location = Point.Empty;

        IntPtr window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        uint threadId = NativeMethods.GetWindowThreadProcessId(window, out _);

        var info = new NativeMethods.GuiThreadInfo();
        info.Size = System.Runtime.InteropServices.Marshal.SizeOf(info);

        if (!NativeMethods.GetGUIThreadInfo(threadId, ref info) || info.Focus == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.Rect box = info.CaretRect;

        // An app that does not report a caret leaves this as all zeroes.
        if (box.Left == 0 && box.Top == 0 && box.Right == 0 && box.Bottom == 0)
        {
            return false;
        }

        int height = box.Bottom - box.Top;
        if (height <= 0)
        {
            height = AssumedLineHeight;
        }

        // The rectangle is relative to the control, so turn it into a screen
        // position before anything can be drawn there.
        var point = new Point(box.Left, box.Top + height + DropBelowCaret);
        if (!NativeMethods.ClientToScreen(info.Focus, ref point))
        {
            return false;
        }

        location = point;
        return true;
    }

    // Just below the mouse pointer, for apps that hide their caret.
    // Time O(1).
    private static Point MouseFallback()
    {
        Point mouse = Control.MousePosition;
        return new Point(mouse.X, mouse.Y + 22);
    }
}
