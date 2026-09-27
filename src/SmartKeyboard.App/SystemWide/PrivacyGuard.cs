using System.Text;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// Decides when SmartKeyboard must stop watching the keyboard.
///
/// A program that sees every key on the PC has to be careful. SmartKeyboard
/// never writes typed text to a file, and the word you are typing lives in
/// memory only. But even holding a password in memory for a moment, or showing
/// it in a popup, is not acceptable. So it stops completely when a password is
/// likely to be on screen.
///
/// Two checks:
///   1. the focused control is a real password box, asked directly, or
///   2. the window title mentions a password or signing in, which is the
///      only thing that works for a browser.
///
/// Neither is perfect. Check 1 only works for standard Windows controls, and
/// a browser's password field cannot be seen this way. Check 2 catches the
/// common cases by name. When in doubt, this errs towards stopping.
/// </summary>
public static class PrivacyGuard
{
    /// <summary>
    /// Window classes that are secure prompts. Kept deliberately narrow:
    /// the general dialog class "#32770" would match every message box on the
    /// PC and pause the feature constantly.
    /// </summary>
    private static readonly string[] SensitiveClassNames =
    {
        "credential dialog xaml host",
    };

    // True when typing should not be watched right now.
    // Time O(1) plus the length of the window title.
    public static bool ShouldPause()
    {
        return Look() ?? true;
    }

    /// <summary>
    /// True or false when we managed to look, null when the check itself
    /// failed and we learned nothing.
    ///
    /// The difference matters. A failed reading used to be reported as "a
    /// password is on screen", which paused SmartKeyboard, threw away the
    /// word being typed and cost the user a correction. Erring towards
    /// pausing is right when we looked and are unsure; it is not right when
    /// we did not manage to look at all, because that turns a passing glitch
    /// into lost work.
    /// </summary>
    // Time O(1) plus the length of the window title.
    public static bool? Look()
    {
        try
        {
            if (IsPasswordBoxFocused())
            {
                return true;
            }

            return TitleLooksSensitive(CaretLocator.GetForegroundWindowTitle());
        }
        catch (Exception)
        {
            return null;
        }
    }

    // True when the app in front is a code editor or a terminal.
    //
    // Asked on the same timer as the password check, never on the hook
    // thread, because reading another program's name is not free.
    // Time O(1) plus one lookup by process id.
    public static bool IsCodeWindow()
    {
        return SmartKeyboard.Core.Engine.CodeWindowDetector.IsCodeWindow(GetForegroundProcessName());
    }

    /// <summary>
    /// The name of the program that owns the window in front, or empty.
    ///
    /// The name is used rather than the window title because a title changes
    /// with whatever file is open, while a name does not change at all while
    /// the program runs.
    /// </summary>
    // Time O(1) plus one lookup by process id.
    public static string GetForegroundProcessName()
    {
        try
        {
            IntPtr window = NativeMethods.GetForegroundWindow();
            if (window == IntPtr.Zero)
            {
                return string.Empty;
            }

            NativeMethods.GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0)
            {
                return string.Empty;
            }

            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception)
        {
            // The program closed, or it is one we are not allowed to ask
            // about. Either way we simply do not know, and not knowing must
            // not stop SmartKeyboard working everywhere else.
            return string.Empty;
        }
    }

    /// <summary>True when the window title mentions passwords or signing in.</summary>
    // The matching itself lives in Core so it can be tested without Windows.
    // Time O(L) over the title.
    public static bool TitleLooksSensitive(string? title)
    {
        return SmartKeyboard.Core.Engine.SensitiveWindowDetector.LooksSensitive(title);
    }

    // Asks the focused control whether it hides what is typed into it.
    // A real password box answers with the character it shows instead, usually
    // a dot. Anything else answers with zero.
    //
    // Two rules learned the hard way.
    //
    // Only a genuine Edit control is asked. Chromium and Electron windows do
    // not understand the question, and whatever they answer is meaningless.
    // Trusting that answer made SmartKeyboard think every browser text box was
    // a password box, and it wiped the word being typed on every key.
    //
    // And the question is asked with a timeout. A plain SendMessage waits for
    // the other program to reply, which can take as long as that program
    // likes. That is never acceptable near the keyboard.
    // Time O(1), with a hard limit of TimeoutMs.
    private static bool IsPasswordBoxFocused()
    {
        IntPtr control = CaretLocator.GetFocusedControl();
        if (control == IntPtr.Zero)
        {
            return false;
        }

        string className = GetClassName(control);

        if (LooksLikeSecurePrompt(className))
        {
            return true;
        }

        if (!IsEditControl(className))
        {
            return false;
        }

        IntPtr sent = NativeMethods.SendMessageTimeout(
            control,
            NativeMethods.EM_GETPASSWORDCHAR,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG,
            TimeoutMs,
            out IntPtr masked);

        // A zero return means the call timed out or failed, so we learned
        // nothing and must not guess.
        return sent != IntPtr.Zero && masked != IntPtr.Zero;
    }

    /// <summary>How long to wait for another program to answer, at most.</summary>
    private const uint TimeoutMs = 50;

    // True for the standard Windows text box classes, the only ones that
    // understand EM_GETPASSWORDCHAR. Time O(1).
    private static bool IsEditControl(string className)
    {
        return className.Equals("edit", StringComparison.Ordinal)
            || className.StartsWith("richedit", StringComparison.Ordinal)
            || className.Contains("textbox", StringComparison.Ordinal);
    }

    // Reads the class name of a window, in lower case. Time O(1).
    private static string GetClassName(IntPtr control)
    {
        var name = new StringBuilder(256);

        return NativeMethods.GetClassName(control, name, name.Capacity) == 0
            ? string.Empty
            : name.ToString().ToLowerInvariant();
    }

    // Checks the window class, which catches the Windows credential prompt.
    // Time O(1).
    private static bool LooksLikeSecurePrompt(string className)
    {
        foreach (string sensitive in SensitiveClassNames)
        {
            if (className.Contains(sensitive, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
