using SmartKeyboard.App.SystemWide;

namespace SmartKeyboard.App;

internal static class Program
{
    /// <summary>Pass this on the command line to open only the editor.</summary>
    private const string EditorOnlyFlag = "--editor";

    /// <summary>
    /// The name of the lock that keeps a second copy from starting.
    ///
    /// Two copies is not a tidiness problem, it corrupts what the user types.
    /// Each one installs its own keyboard hook, so both see the same "helo"
    /// and both try to fix it. The first deletes four letters and types
    /// "hello"; the second then deletes four letters of THAT and types
    /// "hello" again, leaving "hhello". A copy that started mid word has an
    /// empty tracker, deletes nothing, and simply appends, leaving
    /// "helohello". Both are exactly what was reported.
    /// </summary>
    private const string InstanceLockName = "SmartKeyboard.SingleInstance.v1";

    [STAThread]
    private static void Main(string[] args)
    {
        // Taken before anything else, so a second copy spends no time reading
        // the dictionary only to find out it is not wanted.
        using var instanceLock = new Mutex(true, InstanceLockName, out bool isOnlyCopy);

        if (!isOnlyCopy)
        {
            // Step aside, and nudge the copy that is already running into
            // showing itself. The user almost certainly started this one
            // because they could not tell the first was there.
            NativeMethods.PostMessage(
                NativeMethods.HWND_BROADCAST,
                NativeMethods.AlreadyRunningMessage,
                IntPtr.Zero,
                IntPtr.Zero);
            return;
        }

        ApplicationConfiguration.Initialize();

        // The brand fonts must be loaded before any window is built.
        BrandTheme.Load();

        // The settings file is tiny, so it is read here and handed on. The
        // dictionary is not: that is megabytes of text, and reading it before
        // the first window appears is what made startup look like nothing was
        // happening. TrayController loads it in the background instead.
        AppSettings settings = AppSettings.Load();

        if (args.Contains(EditorOnlyFlag, StringComparer.OrdinalIgnoreCase))
        {
            RunEditorOnly(settings);
        }
        else
        {
            // Normally the tray icon owns the program, so closing the editor
            // does not stop SmartKeyboard working in other apps.
            Application.Run(new TrayController(settings));
        }

        // The lock has to be held for the whole run, so it must still be
        // alive here. Without this the collector is free to release it early.
        GC.KeepAlive(instanceLock);
    }

    // Opens just the editor, with no tray icon and no keyboard watching.
    // Here the wait is unavoidable: the editor has nothing to show until the
    // dictionary is in memory.
    private static void RunEditorOnly(AppSettings settings)
    {
        try
        {
            Application.Run(new Editor.MainForm(AppServices.Start(settings)));
        }
        catch (FileNotFoundException error)
        {
            // The dictionary files are missing, so say so plainly instead of
            // showing a crash dialog.
            MessageBox.Show(
                error.Message,
                "SmartKeyboard could not start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
