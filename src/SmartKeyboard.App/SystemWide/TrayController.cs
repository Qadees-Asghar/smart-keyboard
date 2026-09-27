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

    private readonly AppSettings _settings;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _autocorrectItem;
    private readonly ToolStripMenuItem _editorItem;
    private readonly ToolStripMenuItem _settingsItem;

    // Null until the dictionary has finished loading in the background.
    // Everything that needs it checks first, because the icon is deliberately
    // on screen before it exists.
    private AppServices? _services;

    private SystemWideController? _controller;
    private MainForm? _editor;

    public TrayController(AppSettings settings)
    {
        _settings = settings;

        // Never shown. Windows still needs it to exist to deliver the hotkey.
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        Size = new Size(1, 1);

        _statusItem = new ToolStripMenuItem("Loading dictionary...") { Enabled = false };
        _pauseItem = new ToolStripMenuItem("Pause (Ctrl+Alt+K)");
        _pauseItem.Click += (_, _) => TogglePause();

        // Autocorrect in other apps starts off, because changing someone
        // else's text without being asked is not a good default. It is put
        // right here so it is one click away rather than buried in Settings.
        _autocorrectItem = new ToolStripMenuItem("Fix typos in other apps")
        {
            CheckOnClick = true,
            Checked = _settings.SystemWideAutocorrect,
        };

        _autocorrectItem.CheckedChanged += (_, _) =>
        {
            _settings.SystemWideAutocorrect = _autocorrectItem.Checked;
            _settings.Save();
            UpdateMenu();
        };

        // These three stay greyed out until the dictionary is in memory.
        // There is nothing for them to open before that.
        _pauseItem.Enabled = false;
        _autocorrectItem.Enabled = false;

        _editorItem = new ToolStripMenuItem("Open Editor") { Enabled = false };
        _editorItem.Click += (_, _) => ShowEditor();

        _settingsItem = new ToolStripMenuItem("Settings...") { Enabled = false };
        _settingsItem.Click += (_, _) => ShowSettings();

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
        _menu.Items.Add(_editorItem);
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exit);

        _tray = new NotifyIcon
        {
            Icon = BuildIcon(),
            Visible = true,
            Text = "SmartKeyboard, loading...",
            ContextMenuStrip = _menu,
        };

        _tray.DoubleClick += (_, _) => ShowEditor();
    }

    // The window exists, so claim the hotkey and start loading.
    //
    // The dictionary is read on a background thread on purpose. Reading it
    // here would hold the icon back for seconds on a cold machine, and a tray
    // program with no icon looks like a program that did not start, which is
    // why people double clicked run.cmd and ended up with two copies.
    // Time O(1) here.
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        NativeMethods.RegisterHotKey(
            Handle,
            HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT,
            (int)Keys.K);

        UpdateMenu();
        StartLoading();
    }

    // Reads the dictionary away from the UI thread, then comes back to it.
    // Time O(1) here, the real work is on the other thread.
    private void StartLoading()
    {
        Task.Run(() => AppServices.Start(_settings)).ContinueWith(
            finished =>
            {
                try
                {
                    BeginInvoke(() => OnLoaded(finished));
                }
                catch (InvalidOperationException)
                {
                    // The user exited while it was still loading.
                }
            },
            TaskScheduler.Default);
    }

    // The dictionary is in memory. Wire everything up and open the menu.
    // Runs on the UI thread. Time O(1).
    private void OnLoaded(Task<AppServices> finished)
    {
        if (finished.IsFaulted)
        {
            Exception error = finished.Exception?.GetBaseException()
                ?? new InvalidOperationException("The dictionary could not be loaded.");

            _statusItem.Text = "Could not load the dictionary";
            _tray.Text = "SmartKeyboard, could not start";
            _tray.ShowBalloonTip(8000, "SmartKeyboard could not start", error.Message, ToolTipIcon.Error);
            return;
        }

        _services = finished.Result;

        _pauseItem.Enabled = true;
        _autocorrectItem.Enabled = true;
        _editorItem.Enabled = true;
        _settingsItem.Enabled = true;

        StartSystemWide();
        UpdateMenu();
    }

    // Starts watching the keyboard. Time O(1).
    private void StartSystemWide()
    {
        if (_services is null)
        {
            return;
        }

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
    }

    // Catches the global hotkey, and the word from a second copy that it
    // tried to start. Time O(1).
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WM_HOTKEY && message.WParam.ToInt32() == HotkeyId)
        {
            TogglePause();
            return;
        }

        if (message.Msg == (int)NativeMethods.AlreadyRunningMessage)
        {
            ShowAlreadyRunning();
            return;
        }

        base.WndProc(ref message);
    }

    // Someone started SmartKeyboard while this copy was already running. The
    // other one has already closed itself, so all that is left is to say so,
    // because not knowing it was running is why they started it.
    // Time O(1).
    private void ShowAlreadyRunning()
    {
        _tray.ShowBalloonTip(
            4000,
            "SmartKeyboard is already running",
            _services is null
                ? "It is still loading the dictionary. This icon is it."
                : "This icon down by the clock is it. Right click for the menu.",
            ToolTipIcon.Info);
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
            // Still loading. The icon is up so the user can see we are here.
            _statusItem.Text = "Loading dictionary...";
            _tray.Text = "SmartKeyboard, loading...";
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
        if (_services is null)
        {
            // Still loading. The editor has nothing to show without words.
            return;
        }

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
        if (_services is null)
        {
            return;
        }

        using var window = new SettingsForm(_settings);
        if (window.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settings.CopyFrom(window.Result);
        _settings.Save();
        _services.ApplySettings();

        _autocorrectItem.Checked = _settings.SystemWideAutocorrect;
    }

    // Time O(1). The saving happens in OnFormClosing, so that it happens
    // however the program is closed and not only from this menu item.
    private void ExitProgram()
    {
        Close();
    }

    // Saves what was learned and lets go of the keyboard.
    //
    // This is an override rather than part of the Exit menu item on purpose.
    // run.cmd now closes a running copy before it builds, and Windows closes
    // everything at shut down, and neither of those goes through the menu. Put
    // here, the counts survive all three. Time O(N) over what was learned.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Null when the user quit before the dictionary finished loading.
        // Nothing has been learned yet then, so there is nothing to save.
        _services?.Learning.Save();

        _tray.Visible = false;
        _controller?.Stop();

        base.OnFormClosing(e);
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
