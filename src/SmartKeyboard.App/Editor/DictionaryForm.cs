using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.App.Editor;

/// <summary>
/// A small window to see, add, and remove the words you taught SmartKeyboard.
/// Every change is saved to user_dict.txt right away.
/// </summary>
public class DictionaryForm : Form
{
    private readonly UserDictionary _users;
    private readonly ListBox _list;
    private readonly TextBox _newWord;
    private readonly Button _addButton;
    private readonly Button _removeButton;
    private readonly Label _message;

    public DictionaryForm(UserDictionary users)
    {
        _users = users;

        Text = "My Dictionary";
        Width = 440;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = BrandTheme.Ui(9.5F);
        BackColor = BrandTheme.Light;
        ForeColor = BrandTheme.Dark;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var help = new Label
        {
            Text = "Words here are treated as spelled correctly, and can be suggested while you type.",
            AutoSize = false,
            Height = 44,
            Dock = DockStyle.Fill,
            ForeColor = BrandTheme.Dark
        };

        _newWord = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = BrandTheme.Text(11F),
            BackColor = BrandTheme.Light,
            ForeColor = BrandTheme.Dark,
            BorderStyle = BorderStyle.FixedSingle
        };

        _newWord.KeyDown += OnNewWordKeyDown;

        _addButton = BrandButton("Add", primary: true);
        _addButton.Click += (_, _) => AddTypedWord();

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            Font = BrandTheme.Text(11F),
            BackColor = BrandTheme.Light,
            ForeColor = BrandTheme.Dark,
            BorderStyle = BorderStyle.FixedSingle
        };

        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.KeyDown += OnListKeyDown;

        _removeButton = BrandButton("Remove", primary: false);
        _removeButton.Enabled = false;
        _removeButton.Click += (_, _) => RemoveSelectedWord();

        _message = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 26,
            ForeColor = BrandTheme.Orange
        };

        layout.Controls.Add(help, 0, 0);
        layout.SetColumnSpan(help, 2);
        layout.Controls.Add(_list, 0, 1);
        layout.Controls.Add(_removeButton, 1, 1);
        layout.Controls.Add(_newWord, 0, 2);
        layout.Controls.Add(_addButton, 1, 2);
        layout.Controls.Add(_message, 0, 3);
        layout.SetColumnSpan(_message, 2);

        Controls.Add(layout);

        RefreshList();
    }

    // Builds a button in the brand style. The main action is orange, the
    // others are plain. Time O(1).
    private static Button BrandButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Font = BrandTheme.Ui(9.5F),
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(10, 4, 10, 4),
            BackColor = primary ? BrandTheme.Orange : BrandTheme.LightGray,
            ForeColor = primary ? BrandTheme.Light : BrandTheme.Dark
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    // Fills the list from the user dictionary. Time O(N log N) for the sort.
    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (string word in _users.GetAll())
        {
            _list.Items.Add(word);
        }

        _list.EndUpdate();

        Text = _users.Count == 0
            ? "My Dictionary"
            : $"My Dictionary ({_users.Count} words)";

        UpdateButtons();
    }

    // Time O(1).
    private void UpdateButtons()
    {
        _removeButton.Enabled = _list.SelectedItem is not null;
    }

    private void OnNewWordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            AddTypedWord();
            e.SuppressKeyPress = true;
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            RemoveSelectedWord();
        }
    }

    // Adds whatever is in the box, and says why if it cannot.
    // Time is the same as UserDictionary.Add.
    private void AddTypedWord()
    {
        string word = _newWord.Text.Trim();

        if (word.Length == 0)
        {
            return;
        }

        if (!UserDictionary.IsAllowed(word))
        {
            _message.Text = "Only single words with letters are allowed.";
            return;
        }

        if (!_users.Add(word))
        {
            _message.Text = $"\"{word.ToLowerInvariant()}\" is already in your dictionary.";
            return;
        }

        _message.Text = $"Added \"{word.ToLowerInvariant()}\".";
        _newWord.Clear();
        RefreshList();
        WordsChanged?.Invoke(this, EventArgs.Empty);
    }

    // Removes the highlighted word. Time is the same as UserDictionary.Remove.
    private void RemoveSelectedWord()
    {
        if (_list.SelectedItem is not string word)
        {
            return;
        }

        _users.Remove(word);
        _message.Text = $"Removed \"{word}\".";
        RefreshList();
        WordsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised whenever a word is added or removed, so the editor can repaint.</summary>
    public event EventHandler? WordsChanged;
}
