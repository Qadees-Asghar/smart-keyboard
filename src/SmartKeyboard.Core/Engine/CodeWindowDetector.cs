namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Decides whether the app in front is a code editor or a terminal.
///
/// SmartKeyboard has no business in either. Code is not English, so "fixing"
/// it is wrong by definition, and the help gets in the way badly: the popup
/// takes Enter when you wanted a new line, and it takes Up and Down when you
/// wanted to move between lines. In an editor the popup is showing almost all
/// the time, so almost every one of those keys goes missing.
///
/// The test is the process name, not the window title. A title changes with
/// whatever file is open and often carries the project name, so matching on it
/// would be both unreliable and a way to catch the wrong app. A process name
/// does not change while the program runs.
///
/// This is plain string matching with no Windows in it, so it lives in Core
/// and is tested properly, exactly like the password check in
/// SensitiveWindowDetector.
/// </summary>
public static class CodeWindowDetector
{
    /// <summary>
    /// Process names to stay out of, without the ".exe". Matched whole, not as
    /// a fragment, so short names like "code" and "vim" are safe: they cannot
    /// accidentally match "codesign" or "vimeo".
    /// </summary>
    public static readonly string[] CodeWindowNames =
    {
        // Editors and IDEs
        "antigravity ide",
        "code",
        "code - insiders",
        "codium",
        "vscodium",
        "cursor",
        "windsurf",
        "zed",
        "devenv",
        "rider64",
        "idea64",
        "pycharm64",
        "webstorm64",
        "clion64",
        "goland64",
        "phpstorm64",
        "studio64",
        "sublime_text",
        "notepad++",
        "vim",
        "gvim",
        "nvim",
        "emacs",

        // Terminals and shells
        "windowsterminal",
        "openconsole",
        "conhost",
        "cmd",
        "powershell",
        "pwsh",
        "mintty",
        "alacritty",
        "wezterm-gui",
        "putty",
        "kitty",
        "hyper",
        "tabby",
    };

    // True when this process is a code editor or a terminal.
    // Time O(N) over the list, which is tiny, and it runs on a timer rather
    // than anywhere near the keyboard.
    public static bool IsCodeWindow(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        string name = Trim(processName);

        foreach (string known in CodeWindowNames)
        {
            if (name.Equals(known, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Lowercases the name and drops a trailing ".exe", because some ways of
    // asking Windows include it and some do not. Time O(L).
    private static string Trim(string processName)
    {
        string name = processName.Trim().ToLowerInvariant();

        return name.EndsWith(".exe", StringComparison.Ordinal)
            ? name[..^4]
            : name;
    }
}
