namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// An optional record of how well replacing a word worked, off by default.
///
/// It exists because the failure it is meant to explain cannot be reproduced
/// on a development machine. Whether a replacement lands depends on how the
/// app in front handles input, and the apps where it went wrong, the Store
/// version of Notepad and a Chromium editor, both take input asynchronously.
/// The only way to see what happened is to look on the machine where it
/// happened.
///
/// It writes counts and nothing else. Not the word, not the correction, not
/// a single character of anything typed. That keeps the promise the rest of
/// the program makes: typed text never reaches the disk.
///
/// A line looks like this:
///
///   2026-09-26 14:02:11  app=Notepad  delete=2  type=3  sent=10/10
///
/// "sent" is what Windows accepted against what was offered. If those two
/// differ, Windows itself refused the keystrokes, which usually means the
/// other app is running as Administrator. If they match but the text still
/// comes out wrong, Windows took the keys and the other app dropped them,
/// which is a completely different problem.
/// </summary>
public static class InjectionLog
{
    /// <summary>Where the file is kept, next to the other user files.</summary>
    public static string FilePath => Path.Combine(AppPaths.UserFolder, "inject.log");

    /// <summary>Stop the file growing without limit.</summary>
    private const long MaxBytes = 256 * 1024;

    private static readonly object Lock = new();

    // Adds one line. Does nothing at all when the option is off.
    // Never throws: a problem writing a log must not break typing.
    // Time O(1).
    public static void Record(bool enabled, string appName, TextInjector.InjectionResult result)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            string line = string.Format(
                "{0:yyyy-MM-dd HH:mm:ss}  app={1}  delete={2}  type={3}  sent={4}/{5}",
                DateTime.Now,
                string.IsNullOrEmpty(appName) ? "unknown" : appName,
                result.Deleted,
                result.Typed,
                result.Sent,
                result.Expected);

            Append(line);
        }
        catch (Exception)
        {
            // The folder is gone, or the file is locked. Not worth caring
            // about: this is a diagnostic, not part of the job.
        }
    }

    // Adds one line saying whether the suggestion box was shown, and if not,
    // which rule refused it.
    //
    // This exists because "nothing appears at all" is impossible to tell apart
    // from "appears and is dismissed instantly" by watching, and the failure
    // only happens on the machine it happens on. One line names the guard.
    //
    // Counts only. "len" is how many letters had been typed, never which
    // ones, and the window class is the name of a kind of control, not
    // anything the user wrote.
    // Time O(1), and nothing at all when the option is off.
    public static void RecordPopup(
        bool enabled,
        SmartKeyboard.Core.Engine.SuggestionPolicy.Refusal refusal,
        string kind,
        int prefixLength,
        int words,
        string controlClass)
    {
        if (!enabled)
        {
            return;
        }

        bool shown = refusal == SmartKeyboard.Core.Engine.SuggestionPolicy.Refusal.None;

        string line = string.Format(
            "{0:yyyy-MM-dd HH:mm:ss}  popup={1,-3} kind={2,-10} len={3} words={4} why={5} class={6}",
            DateTime.Now,
            shown ? "yes" : "no",
            kind,
            prefixLength,
            words,
            Describe(refusal),
            string.IsNullOrEmpty(controlClass) ? "none" : controlClass);

        Append(line);
    }

    // Plain words for the log, so the file can be read without the source.
    // Time O(1).
    private static string Describe(SmartKeyboard.Core.Engine.SuggestionPolicy.Refusal refusal)
    {
        return refusal switch
        {
            SmartKeyboard.Core.Engine.SuggestionPolicy.Refusal.NotATextTarget => "not-a-text-target",
            SmartKeyboard.Core.Engine.SuggestionPolicy.Refusal.TooFewLetters => "too-few-letters",
            SmartKeyboard.Core.Engine.SuggestionPolicy.Refusal.NoSession => "not-typing",
            SmartKeyboard.Core.Engine.SuggestionPolicy.Refusal.NoWords => "no-words-found",
            SmartKeyboard.Core.Engine.SuggestionPolicy.Refusal.WordStartUnknown => "word-start-unknown",
            _ => "shown",
        };
    }

    // Puts one line on the end, trimming the file first. Time O(1).
    private static void Append(string line)
    {
        try
        {
            lock (Lock)
            {
                Trim();
                File.AppendAllLines(FilePath, new[] { line });
            }
        }
        catch (Exception)
        {
            // Not worth caring about: this is a diagnostic, not the job.
        }
    }

    // Starts the file again once it gets long. Time O(1).
    private static void Trim()
    {
        var file = new FileInfo(FilePath);

        if (file.Exists && file.Length > MaxBytes)
        {
            file.Delete();
        }
    }
}
