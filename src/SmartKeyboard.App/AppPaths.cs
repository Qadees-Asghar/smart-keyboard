namespace SmartKeyboard.App;

/// <summary>
/// Knows where the program's files live.
///
/// The dictionary files ship with the program, so they sit next to the exe.
/// The user's own files are written to AppData, so a rebuild never wipes them.
/// </summary>
public static class AppPaths
{
    /// <summary>The folder holding words.txt and bigrams.txt.</summary>
    public static string DictionaryFolder =>
        Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>The folder holding user_dict.txt and learned.txt.</summary>
    public static string UserFolder
    {
        get
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartKeyboard");

            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string WordsFile => Path.Combine(DictionaryFolder, "words.txt");

    public static string BigramsFile => Path.Combine(DictionaryFolder, "bigrams.txt");

    public static string UserDictionaryFile => Path.Combine(UserFolder, "user_dict.txt");

    public static string LearnedFile => Path.Combine(UserFolder, "learned.txt");
}
