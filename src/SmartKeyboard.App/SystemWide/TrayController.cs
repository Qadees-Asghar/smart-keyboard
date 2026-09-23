using SmartKeyboard.App.Editor;

namespace SmartKeyboard.App.SystemWide;

/// <summary>
/// The tray icon down by the clock, and the global Ctrl+Alt+K hotkey.
///
/// This is also the window that owns the whole program. It is never shown: it
/// exists so there is something on the UI thread to hand work back to, and
/// something for Windows to send the hotkey message to. The editor is opened
/// from the menu rather than being the main window, so closing the editor does
/// not close SmartKeyboard.
/// </summary>
public class TrayController : Form
{
    /// <summary>Any number will do, it just has to be ours.</summary>
    private const int HotkeyId = 0xA17;

    private readonly AppServices _services;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _autocorrectItem;

    private SystemWideController? _controller;
    private MainForm? _editor;

    public TrayController(AppServices services)
    {
        _services = services;

        // Never shown. Windows still needs it to exist to deliver the hotkey.
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        Size = new Size(1, 1);

        _statusItem = new ToolStripMenuItem("Starting...") { Enabled = false };
        _pauseItem = new ToolStripMenuItem("Pause (Ctrl+Alt+K)");
        _pauseItem.Click += (_, _) => TogglePause();

        // Autocorrect in other apps starts off, because changing someone
        // else's text without being asked is not a good default. It is put
        // right here so it is one click away rather than buried in Settings.
        _autocorrectItem = new ToolStripMenuItem("Fix typos in other apps")
        {
            CheckOnClick = true,
            Checked = _services.Settings.SystemWideAutocorrect,
        };

        _autocorrectItem.CheckedChanged += (_, _) =>
        {
            _services.Settings.SystemWideAutocorrect = _autocorrectItem.Checked;
            _services.Settings.Save();
            UpdateMenu();
        };

        var openEditor = new ToolStripMenuItem("Open Editor");
        openEditor.Click += (_, _) => ShowEditor();

        var settings = new ToolStripMenuItem("Settings...");
        settings.Click += (_, _) => ShowSettings();

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitProgram();

        _menu = new ContextMenuStrip
        {
            Font = BrandTheme.Ui(9.5F),
            BackColor = BrandTheme.Light,
            ForeColor = BrandTheme.Dark,
        };

        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(_autocorrectItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(openEditor);
        _menu.Items.Add(settings);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exit);

        _tray = new NotifyIcon
        {
            Icon = BuildIcon(),
            Visible = true,
            Text = "SmartKeyboard",
            ContextMenuStrip = _menu,
        };

        _tray.DoubleClick += (_, _) => ShowEditor();
    }

    // Starts System Wide Mode once the window exists, because the controller
    // needs a real handle to hand work back to. Time O(1).
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        NativeMethods.RegisterHotKey(
            Handle,
            HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT,
            (int)Keys.K);

        _controller = new SystemWideController(_services, this);
        _controller.StateChanged += (_, _) => BeginInvoke(UpdateMenu);
        _controller.TypingBlocked += (_, _) => BeginInvoke(ShowTypingBlockedWarning);

        try
        {
            _controller.Start();
        }
        catch (InvalidOperationException error)
        {
            _tray.ShowBalloonTip(
                5000,
                "SmartKeyboard",
                "System Wide Mode could not start. " + error.Message,
                ToolTipIcon.Warning);
        }

        UpdateMenu();
    }

    // Catches the global hotkey message. Time O(1).
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WM_HOTKEY && message.WParam.ToInt32() == HotkeyId)
        {
            TogglePause();
            return;
        }

        base.WndProc(ref message);
    }

    // Time O(1).
    private void TogglePause()
    {
        _controller?.TogglePause();
        UpdateMenu();
    }

    // Keeps the menu and the tooltip saying the truth. Time O(1).
    private void UpdateMenu()
    {
        if (_controller is null)
        {
            return;
        }

        _pauseItem.Text = _controller.IsPaused
            ? "Resume (Ctrl+Alt+K)"
            : "Pause (Ctrl+Alt+K)";

        _statusItem.Text = _controller.StatusText;

        // A tray tooltip is cut off after 63 characters by Windows.
        string tip = "SmartKeyboard. " + _controller.StatusText;
        _tray.Text = tip.Length > 63 ? tip[..63] : tip;
    }

    // Tells the user why a word would not go in. Windows blocks input sent
    // from a normal program to one running as Administrator, and there is no
    // way around it other than running SmartKeyboard as Administrator too.
    // Time O(1).
    private void ShowTypingBlockedWarning()
    {
        _tray.ShowBalloonTip(
            6000,
            "SmartKeyboard",
            "Windows would not let the word be typed into that app. Apps running as "
                + "Administrator only accept it if SmartKeyboard is run as Administrator as well.",
            ToolTipIcon.Warning);
    }

    // Opens the editor, or brings it back if it is already open. Time O(1).
    private void ShowEditor()
    {
        if (_editor is null || _editor.IsDisposed)
        {
            _editor = new MainForm(_services);
            _editor.FormClosed += (_, _) => _editor = null;
            _editor.Show();
        }
        else
        {
            _editor.WindowState = FormWindowState.Normal;
            _editor.BringToFront();
            _editor.Activate();
        }
    }

    // Time O(1).
    private void ShowSettings()
    {
        using var window = new SettingsForm(_services.Settings);
        if (window.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _services.Settings.CopyFrom(window.Result);
        _services.Settings.Save();
        _services.ApplySettings();

        _autocorrectItem.Checked = _services.Settings.SystemWideAutocorrect;
    }

    // Saves what was learned, then closes everything down. Time O(N).
    private void ExitProgram()
    {
        _services.Learning.Save();

        _tray.Visible = false;
        _controller?.Stop();

        Application.Exit();
    }

    // Draws a small tray icon in the brand orange, so nothing has to ship as
    // an .ico file. Time O(1).
    private static Icon BuildIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var background = new SolidBrush(BrandTheme.Orange);
            graphics.FillRectangle(background, 1, 6, 30, 20);

            // Three "keys" to suggest a keyboard.
            using var key = new SolidBrush(BrandTheme.Light);
            graphics.FillRectangle(key, 5, 11, 6, 5);
            graphics.FillRectangle(key, 13, 11, 6, 5);
            graphics.FillRectangle(key, 21, 11, 6, 5);
            graphics.FillRectangle(key, 8, 19, 16, 4);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _controller?.Dispose();
            _tray.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}
