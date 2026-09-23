namespace SmartKeyboard.App;

/// <summary>
/// The options the user can change, saved between runs.
///
/// The file is plain "name=value" text, one per line, in the same spirit as
/// words.txt. It can be opened in Notepad and read by a human, and a line that
/// makes no sense is skipped instead of stopping the program.
/// </summary>
public class AppSettings
{
    /// <summary>Fix clear typos when a word is finished.</summary>
    public bool AutocorrectEnabled { get; set; } = true;

    /// <summary>Also fix words with two mistakes, when they are long enough.</summary>
    public bool FixMessyWords { get; set; } = true;

    /// <summary>
    /// Autocorrect while typing in other apps. Off by default on purpose:
    /// changing words inside someone else's app without being asked is rude,
    /// so the user has to turn it on.
    /// </summary>
    public bool SystemWideAutocorrect { get; set; }

    /// <summary>Count what you type so suggestions get better.</summary>
    public bool LearningEnabled { get; set; } = true;

    /// <summary>Offer likely next words after a space.</summary>
    public bool ShowPredictions { get; set; } = true;

    /// <summary>Mark unknown words in red.</summary>
    public bool SpellCheckEnabled { get; set; } = true;

    private int _suggestionCount = 5;

    /// <summary>How many suggestions to show, between 1 and 10.</summary>
    public int SuggestionCount
    {
        get => _suggestionCount;
        set => _suggestionCount = Math.Clamp(value, 1, 10);
    }

    /// <summary>Where the file is kept.</summary>
    public static string FilePath => Path.Combine(AppPaths.UserFolder, "settings.txt");

    // Reads the saved options. Anything missing keeps its default, so a new
    // option added later does not break an old settings file.
    // Time O(N) over the lines in the file.
    public static AppSettings Load()
    {
        var settings = new AppSettings();

        if (!File.Exists(FilePath))
        {
            return settings;
        }

        foreach (string line in File.ReadAllLines(FilePath))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            int equals = trimmed.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            string name = trimmed[..equals].Trim();
            string value = trimmed[(equals + 1)..].Trim();

            settings.Apply(name, value);
        }

        return settings;
    }

    // Writes the options out. Time O(1), there are only a few of them.
    public void Save()
    {
        string[] lines =
        {
            "# SmartKeyboard settings. Delete this file to go back to the defaults.",
            $"autocorrect={AutocorrectEnabled}",
            $"fixMessyWords={FixMessyWords}",
            $"systemWideAutocorrect={SystemWideAutocorrect}",
            $"learning={LearningEnabled}",
            $"showPredictions={ShowPredictions}",
            $"spellCheck={SpellCheckEnabled}",
            $"suggestionCount={SuggestionCount}",
        };

        File.WriteAllLines(FilePath, lines);
    }

    // Sets one option from a saved line. Unknown names are ignored.
    // Time O(1).
    private void Apply(string name, string value)
    {
        switch (name)
        {
            case "autocorrect":
                AutocorrectEnabled = ReadBool(value, AutocorrectEnabled);
                break;
            case "fixMessyWords":
                FixMessyWords = ReadBool(value, FixMessyWords);
                break;
            case "systemWideAutocorrect":
                SystemWideAutocorrect = ReadBool(value, SystemWideAutocorrect);
                break;
            case "learning":
                LearningEnabled = ReadBool(value, LearningEnabled);
                break;
            case "showPredictions":
                ShowPredictions = ReadBool(value, ShowPredictions);
                break;
            case "spellCheck":
                SpellCheckEnabled = ReadBool(value, SpellCheckEnabled);
                break;
            case "suggestionCount":
                SuggestionCount = int.TryParse(value, out int count) ? count : SuggestionCount;
                break;
        }
    }

    // Reads true or false, keeping the old value when the text is nonsense.
    // Time O(1).
    private static bool ReadBool(string value, bool fallback)
    {
        return bool.TryParse(value, out bool parsed) ? parsed : fallback;
    }

    /// <summary>A copy, so a Settings window can be cancelled without harm.</summary>
    // Time O(1).
    public AppSettings Copy()
    {
        return new AppSettings
        {
            AutocorrectEnabled = AutocorrectEnabled,
            FixMessyWords = FixMessyWords,
            SystemWideAutocorrect = SystemWideAutocorrect,
            LearningEnabled = LearningEnabled,
            ShowPredictions = ShowPredictions,
            SpellCheckEnabled = SpellCheckEnabled,
            SuggestionCount = SuggestionCount,
        };
    }

    /// <summary>Copies another set of options into this one.</summary>
    // Time O(1).
    public void CopyFrom(AppSettings other)
    {
        AutocorrectEnabled = other.AutocorrectEnabled;
        FixMessyWords = other.FixMessyWords;
        SystemWideAutocorrect = other.SystemWideAutocorrect;
        LearningEnabled = other.LearningEnabled;
        ShowPredictions = other.ShowPredictions;
        SpellCheckEnabled = other.SpellCheckEnabled;
        SuggestionCount = other.SuggestionCount;
    }
}
