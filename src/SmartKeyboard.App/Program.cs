using SmartKeyboard.App.SystemWide;

namespace SmartKeyboard.App;

internal static class Program
{
    /// <summary>Pass this on the command line to open only the editor.</summary>
    private const string EditorOnlyFlag = "--editor";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // The brand fonts must be loaded before any window is built.
        BrandTheme.Load();

        try
        {
            AppServices services = AppServices.Start();

            if (args.Contains(EditorOnlyFlag, StringComparer.OrdinalIgnoreCase))
            {
                // Editor only, with no tray icon and no keyboard watching.
                Application.Run(new Editor.MainForm(services));
                return;
            }

            // Normally the tray icon owns the program, so closing the editor
            // does not stop SmartKeyboard working in other apps.
            Application.Run(new TrayController(services));
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
