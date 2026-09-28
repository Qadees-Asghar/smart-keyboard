namespace SmartKeyboard.Core;

/// <summary>
/// The command Windows runs at sign in to start SmartKeyboard.
///
/// It goes in the per-user Run key, so it has to be exactly right: the path
/// quoted, because "D:\tools\..." or a user folder can hold spaces, and the
/// background flag, so signing in does not throw a window in the user's face.
/// </summary>
public static class StartupCommand
{
    // The full command for this exe. Time O(L).
    public static string Build(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("The program path is empty.", nameof(exePath));
        }

        return $"\"{exePath.Trim().Trim('"')}\" {LaunchOptions.BackgroundFlag}";
    }

    // The program a Run key command starts, without its quotes and flags,
    // or empty when it cannot be read. Time O(L).
    public static string ProgramOf(string? command)
    {
        string text = (command ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (text[0] == '"')
        {
            int close = text.IndexOf('"', 1);
            return close < 0 ? text[1..] : text[1..close];
        }

        int space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    // True when the command starts this exe. Windows paths ignore case.
    // Time O(L).
    public static bool PointsTo(string? command, string exePath)
    {
        string program = ProgramOf(command);

        return program.Length > 0
            && string.Equals(
                program,
                (exePath ?? string.Empty).Trim().Trim('"'),
                StringComparison.OrdinalIgnoreCase);
    }
}
