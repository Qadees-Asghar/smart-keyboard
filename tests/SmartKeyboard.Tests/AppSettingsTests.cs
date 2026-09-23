namespace SmartKeyboard.Tests;

/// <summary>
/// The settings file is plain text so a person can read and fix it by hand.
/// These tests are about being forgiving: a missing file, a missing line, or
/// a line full of nonsense must never stop the program from starting.
///
/// AppSettings lives in the app project, so the same reading and writing is
/// repeated here against a temporary file to keep the test project free of a
/// Windows Forms reference.
/// </summary>
public class AppSettingsTests
{
    private sealed class Settings
    {
        public bool Autocorrect { get; set; } = true;
        public bool FixMessyWords { get; set; } = true;
        public bool SystemWideAutocorrect { get; set; }
        public bool Learning { get; set; } = true;

        private int _count = 5;

        public int SuggestionCount
        {
            get => _count;
            set => _count = Math.Clamp(value, 1, 10);
        }

        public static Settings Load(string path)
        {
            var settings = new Settings();
            if (!File.Exists(path))
            {
                return settings;
            }

            foreach (string line in File.ReadAllLines(path))
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

                switch (name)
                {
                    case "autocorrect":
                        settings.Autocorrect = bool.TryParse(value, out bool a) ? a : settings.Autocorrect;
                        break;
                    case "fixMessyWords":
                        settings.FixMessyWords = bool.TryParse(value, out bool m) ? m : settings.FixMessyWords;
                        break;
                    case "systemWideAutocorrect":
                        settings.SystemWideAutocorrect = bool.TryParse(value, out bool w) ? w : settings.SystemWideAutocorrect;
                        break;
                    case "learning":
                        settings.Learning = bool.TryParse(value, out bool l) ? l : settings.Learning;
                        break;
                    case "suggestionCount":
                        settings.SuggestionCount = int.TryParse(value, out int c) ? c : settings.SuggestionCount;
                        break;
                }
            }

            return settings;
        }
    }

    [Fact]
    public void AMissingFileGivesTheDefaults()
    {
        using var folder = new TempDataFolder();

        Settings settings = Settings.Load(Path.Combine(folder.Path, "nothing.txt"));

        Assert.True(settings.Autocorrect);
        Assert.True(settings.FixMessyWords);
        Assert.True(settings.Learning);
        Assert.Equal(5, settings.SuggestionCount);
    }

    [Fact]
    public void SystemWideAutocorrectIsOffByDefault()
    {
        using var folder = new TempDataFolder();

        // Changing words inside someone else's app is not something to do
        // without being asked, so this one starts off.
        Assert.False(Settings.Load(Path.Combine(folder.Path, "nothing.txt")).SystemWideAutocorrect);
    }

    [Fact]
    public void SavedValuesAreReadBack()
    {
        using var folder = new TempDataFolder();
        string path = folder.Write(
            "settings.txt",
            "autocorrect=False",
            "learning=False",
            "suggestionCount=8");

        Settings settings = Settings.Load(path);

        Assert.False(settings.Autocorrect);
        Assert.False(settings.Learning);
        Assert.Equal(8, settings.SuggestionCount);
    }

    [Fact]
    public void AMissingLineKeepsItsDefault()
    {
        using var folder = new TempDataFolder();
        string path = folder.Write("settings.txt", "autocorrect=False");

        Settings settings = Settings.Load(path);

        Assert.False(settings.Autocorrect);
        Assert.True(settings.Learning);
        Assert.Equal(5, settings.SuggestionCount);
    }

    [Fact]
    public void NonsenseLinesAreSkippedInsteadOfCrashing()
    {
        using var folder = new TempDataFolder();
        string path = folder.Write(
            "settings.txt",
            "# a comment",
            string.Empty,
            "no equals sign here",
            "=novalue",
            "autocorrect=maybe",
            "suggestionCount=banana",
            "unknownOption=True",
            "learning=False");

        Settings settings = Settings.Load(path);

        // The broken lines kept their defaults, the good line was read.
        Assert.True(settings.Autocorrect);
        Assert.Equal(5, settings.SuggestionCount);
        Assert.False(settings.Learning);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    [InlineData(10, 10)]
    [InlineData(99, 10)]
    public void TheSuggestionCountIsKeptBetweenOneAndTen(int asked, int expected)
    {
        using var folder = new TempDataFolder();
        string path = folder.Write("settings.txt", $"suggestionCount={asked}");

        Assert.Equal(expected, Settings.Load(path).SuggestionCount);
    }
}
