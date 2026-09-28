namespace SmartKeyboard.Core;

/// <summary>How SmartKeyboard was asked to start.</summary>
public enum LaunchMode
{
    /// <summary>
    /// Started by a person, from the desktop icon or the Start menu: the tray
    /// starts and the editor window opens, the way an app is expected to.
    /// </summary>
    Normal,

    /// <summary>
    /// Started by Windows at sign in, or by run.cmd while developing: the
    /// tray starts quietly and no window opens.
    /// </summary>
    Background,

    /// <summary>The editor on its own, with no tray and no keyboard watching.</summary>
    EditorOnly,
}

/// <summary>
/// Reads the command line into a LaunchMode.
///
/// Kept apart from Program so it can be tested. Anything it does not
/// recognise is ignored rather than refused: a shortcut with a stray argument
/// should still start the app.
/// </summary>
public static class LaunchOptions
{
    /// <summary>Start quietly in the tray. Used by Start with Windows.</summary>
    public const string BackgroundFlag = "--background";

    /// <summary>Open only the editor.</summary>
    public const string EditorOnlyFlag = "--editor";

    // Time O(n) over the arguments.
    public static LaunchMode Parse(IEnumerable<string>? args)
    {
        var given = (args ?? Array.Empty<string>())
            .Select(arg => (arg ?? string.Empty).Trim())
            .ToList();

        // The editor on its own wins, because it is the older flag and asks
        // for the most specific thing.
        if (given.Contains(EditorOnlyFlag, StringComparer.OrdinalIgnoreCase))
        {
            return LaunchMode.EditorOnly;
        }

        if (given.Contains(BackgroundFlag, StringComparer.OrdinalIgnoreCase))
        {
            return LaunchMode.Background;
        }

        return LaunchMode.Normal;
    }
}
