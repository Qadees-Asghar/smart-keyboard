namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Decides whether the thing with keyboard focus is somewhere text can be
/// typed at all.
///
/// A low level keyboard hook sees every key on the PC, including keys pressed
/// with a file list, a button or the desktop in focus. Suggesting words there
/// is pointless, and it was the reason the suggestion box appeared when the
/// user was not writing anything.
///
/// Windows does not answer this question directly, so three weaker signals are
/// combined, best first:
///
///   1. A caret is being reported. Nothing reports a caret except a place that
///      takes text, so this settles it on its own.
///   2. The class name of the focused control. Windows text boxes and the
///      common editor controls are known by name, and so are the controls that
///      certainly do not take text.
///   3. Nothing has focus at all, which means there is nowhere for a letter to
///      go.
///
/// Anything not recognised is allowed. That is deliberate: an app where
/// SmartKeyboard silently stops working is worse than an occasional box in the
/// wrong place, and the minimum word length and the idle timeout already
/// remove most of the noise.
///
/// Known limit, and it cannot be fixed from here. Inside a browser this always
/// answers yes, because Chromium reports no caret and hands back one window
/// class for the whole page whether the user is in a text box or not.
///
/// Plain string matching with no Windows in it, so it lives in Core and is
/// tested, the same as SensitiveWindowDetector and CodeWindowDetector.
/// </summary>
public static class TextInputClassifier
{
    /// <summary>
    /// Control classes that take text. Matched as a fragment, because Windows
    /// Forms and WPF add their own suffixes, as in "windowsforms10.edit.app".
    /// </summary>
    public static readonly string[] TextClasses =
    {
        "edit",
        "richedit",
        "textbox",
        "scintilla",
        "textinput",
    };

    /// <summary>
    /// Surfaces where we cannot tell, and choose to allow. A browser draws its
    /// own caret and gives the whole page one class, so refusing here would
    /// switch SmartKeyboard off in the apps people write in most.
    /// </summary>
    public static readonly string[] UnknowableClasses =
    {
        "chrome_renderwidgethosthwnd",
        "chrome_widgetwin",
        "mozillawindowclass",
        "windows.ui.core.corewindow",
    };

    /// <summary>
    /// Controls that certainly do not take typed words. The desktop and the
    /// taskbar are in here, because that is where the stray box came from.
    /// </summary>
    public static readonly string[] NonTextClasses =
    {
        "syslistview32",
        "systreeview32",
        "systabcontrol32",
        "sysheader32",
        "button",
        "listbox",
        "combobox",
        "scrollbar",
        "static",
        "directuihwnd",
        "progman",
        "workerw",
        "shell_traywnd",
        "shelldll_defview",
        "mstaskswwclass",
    };

    // True when a typed letter would go somewhere that takes text.
    //
    // Order matters. A caret beats everything, then the classes that say no,
    // then the classes that say yes, and only then the permissive default.
    // The "no" list is checked before the "yes" list because some no names
    // contain a yes name as a fragment.
    // Time O(N) over the lists, which are tiny.
    public static bool TakesText(bool hasCaret, bool hasFocus, string? controlClass)
    {
        if (hasCaret)
        {
            return true;
        }

        if (!hasFocus)
        {
            return false;
        }

        string name = (controlClass ?? string.Empty).Trim().ToLowerInvariant();

        if (name.Length == 0)
        {
            // Something has focus but will not say what it is. Allowed, for
            // the same reason an unknown class is allowed.
            return true;
        }

        if (Matches(name, NonTextClasses))
        {
            return false;
        }

        return true;
    }

    /// <summary>True when the class is one we know takes text.</summary>
    // Kept separate so the lists can be tested directly. Time O(N).
    public static bool IsKnownTextClass(string? controlClass)
    {
        string name = (controlClass ?? string.Empty).Trim().ToLowerInvariant();

        return Matches(name, TextClasses) || Matches(name, UnknowableClasses);
    }

    // True when the name contains any of the given fragments. Time O(N * L).
    private static bool Matches(string name, string[] fragments)
    {
        foreach (string fragment in fragments)
        {
            if (name.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
