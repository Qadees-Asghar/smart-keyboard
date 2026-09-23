namespace SmartKeyboard.App.Editor;

/// <summary>
/// The Settings window. It edits a copy of the options, so pressing Cancel
/// really does leave everything as it was.
/// </summary>
public class SettingsForm : Form
{
    private readonly AppSettings _working;

    private readonly CheckBox _autocorrect;
    private readonly CheckBox _messy;
    private readonly CheckBox _systemWideAutocorrect;
    private readonly CheckBox _learning;
    private readonly CheckBox _predictions;
    private readonly CheckBox _spellCheck;
    private readonly NumericUpDown _suggestionCount;

    public SettingsForm(AppSettings settings)
    {
        _working = settings.Copy();

        Text = "Settings";
        Width = 460;
        Height = 470;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        Font = BrandTheme.Ui(9.5F);
        BackColor = BrandTheme.Light;
        ForeColor = BrandTheme.Dark;

        _autocorrect = Check(
            "Fix clear typos when I finish a word",
            "Only when one word is the obvious winner. Ctrl+Z puts yours back.",
            _working.AutocorrectEnabled);

        _messy = Check(
            "Also fix messier words",
            "Words of 6 letters or more with two mistakes in them.",
            _working.FixMessyWords);

        _systemWideAutocorrect = Check(
            "Fix typos in other apps too",
            "Off by default. Other apps are not mine to change without asking.",
            _working.SystemWideAutocorrect);

        _spellCheck = Check(
            "Mark unknown words in red",
            "Right click a red word to see suggested fixes.",
            _working.SpellCheckEnabled);

        _predictions = Check(
            "Suggest the next word after a space",
            "Uses the word pairs counted from real sentences.",
            _working.ShowPredictions);

        _learning = Check(
            "Learn from my typing",
            "Counts the words and pairs you use. Never learns a typo.",
            _working.LearningEnabled);

        _suggestionCount = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10,
            Value = _working.SuggestionCount,
            Width = 60,
            Font = BrandTheme.Ui(9.5F),
            BackColor = BrandTheme.Light,
            ForeColor = BrandTheme.Dark,
            BorderStyle = BorderStyle.FixedSingle
        };

        Controls.Add(BuildLayout());
    }

    /// <summary>The options as the user left them. Only valid after OK.</summary>
    public AppSettings Result => _working;

    // Lays the window out in one column, with the buttons at the bottom.
    // Time O(1).
    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoScroll = true,
            Padding = new Padding(16)
        };

        layout.Controls.Add(SectionTitle("Typing"));
        layout.Controls.Add(_autocorrect);
        layout.Controls.Add(_messy);
        layout.Controls.Add(_spellCheck);

        layout.Controls.Add(SectionTitle("Suggestions"));
        layout.Controls.Add(_predictions);
        layout.Controls.Add(CountRow());

        layout.Controls.Add(SectionTitle("Learning"));
        layout.Controls.Add(_learning);

        layout.Controls.Add(SectionTitle("System Wide Mode"));
        layout.Controls.Add(_systemWideAutocorrect);

        layout.Controls.Add(ButtonRow());

        foreach (Control control in layout.Controls)
        {
            control.Margin = new Padding(0, 0, 0, 6);
        }

        return layout;
    }

    // A small heading above a group of options. Time O(1).
    private static Label SectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = BrandTheme.Ui(10F, FontStyle.Bold),
            ForeColor = BrandTheme.Orange,
            Margin = new Padding(0, 10, 0, 4)
        };
    }

    // One option with its explanation underneath. Time O(1).
    private static CheckBox Check(string text, string help, bool value)
    {
        return new CheckBox
        {
            Text = text + Environment.NewLine + help,
            Checked = value,
            AutoSize = false,
            Width = 400,
            Height = 42,
            ForeColor = BrandTheme.Dark,
            FlatStyle = FlatStyle.Flat
        };
    }

    // The "how many suggestions" row. Time O(1).
    private Control CountRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        row.Controls.Add(new Label
        {
            Text = "How many suggestions to show",
            AutoSize = true,
            ForeColor = BrandTheme.Dark,
            Margin = new Padding(0, 6, 8, 0)
        });

        row.Controls.Add(_suggestionCount);
        return row;
    }

    // The OK and Cancel buttons. Time O(1).
    private Control ButtonRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 16, 0, 0)
        };

        Button save = Styled("Save", primary: true);
        save.Click += (_, _) =>
        {
            ReadValues();
            DialogResult = DialogResult.OK;
            Close();
        };

        Button cancel = Styled("Cancel", primary: false);
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        row.Controls.Add(save);
        row.Controls.Add(cancel);

        AcceptButton = save;
        CancelButton = cancel;

        return row;
    }

    // Copies what is on screen into the working copy. Time O(1).
    private void ReadValues()
    {
        _working.AutocorrectEnabled = _autocorrect.Checked;
        _working.FixMessyWords = _messy.Checked;
        _working.SystemWideAutocorrect = _systemWideAutocorrect.Checked;
        _working.LearningEnabled = _learning.Checked;
        _working.ShowPredictions = _predictions.Checked;
        _working.SpellCheckEnabled = _spellCheck.Checked;
        _working.SuggestionCount = (int)_suggestionCount.Value;
    }

    // A button in the brand style. Time O(1).
    private static Button Styled(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Font = BrandTheme.Ui(9.5F),
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(16, 5, 16, 5),
            Margin = new Padding(0, 0, 8, 0),
            BackColor = primary ? BrandTheme.Orange : BrandTheme.LightGray,
            ForeColor = primary ? BrandTheme.Light : BrandTheme.Dark
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
