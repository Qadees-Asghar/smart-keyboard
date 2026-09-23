using SmartKeyboard.Core;
using SmartKeyboard.Core.Data;
using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.App;

/// <summary>
/// Builds the Core objects once and hands them to whoever needs them.
/// Editor Mode and System Wide Mode both use this same set, so there is only
/// one dictionary in memory.
///
/// The Trie is built first because nothing can be suggested without it.
/// The BK tree is built on a background thread, because it takes longer and
/// only typo fixing needs it. The window opens straight away either way.
/// </summary>
public sealed class AppServices
{
    private static AppServices? _current;

    private AppServices(FileWordRepository repository, SuggestionEngine suggestions)
    {
        Repository = repository;
        Suggestions = suggestions;
        Tree = new BKTree();
        Users = new UserDictionary(repository, DictionaryLoader.Instance.Words, Tree);
        Predictions = new NextWordPredictor(
            DictionaryLoader.Instance.Bigrams,
            DictionaryLoader.Instance.Words);

        Learning = new LearningEngine(
            repository,
            DictionaryLoader.Instance.Words,
            DictionaryLoader.Instance.Bigrams,
            Users);

        // Learning changes the word counts, so the cached "most common words"
        // list has to be worked out again.
        Learning.Changed += (_, _) => Predictions.Refresh();
    }

    /// <summary>Raised on a background thread once typo fixing is ready to use.</summary>
    public event EventHandler? SpellCheckReady;

    /// <summary>The services for this run. Call Start first.</summary>
    public static AppServices Current =>
        _current ?? throw new InvalidOperationException("AppServices.Start has not been called yet.");

    public FileWordRepository Repository { get; }

    public SuggestionEngine Suggestions { get; }

    /// <summary>Guesses the next word from the word pairs.</summary>
    public NextWordPredictor Predictions { get; }

    /// <summary>Counts what you type, so suggestions get better.</summary>
    public LearningEngine Learning { get; }

    /// <summary>The options the user chose, saved between runs.</summary>
    public AppSettings Settings { get; } = AppSettings.Load();

    public DictionaryLoader Dictionary => DictionaryLoader.Instance;

    /// <summary>The typo tree. Empty until IsSpellCheckReady turns true.</summary>
    public BKTree Tree { get; }

    /// <summary>Finds fixes for typos. Null until the tree has been built.</summary>
    public FuzzyMatcher? Fuzzy { get; private set; }

    /// <summary>The words the user added by hand.</summary>
    public UserDictionary Users { get; }

    /// <summary>Fixes typos on space. Null until the typo tree has been built.</summary>
    public AutocorrectEngine? Autocorrect { get; private set; }

    /// <summary>True once the BK tree has finished building.</summary>
    public bool IsSpellCheckReady { get; private set; }

    /// <summary>How long building the typo tree took, in milliseconds.</summary>
    public long TreeBuildMs { get; private set; }

    // Reads the dictionary files, then starts building the typo tree in the
    // background. Time is the same as DictionaryLoader.Load.
    public static AppServices Start()
    {
        var repository = new FileWordRepository(
            AppPaths.WordsFile,
            AppPaths.BigramsFile,
            AppPaths.UserDictionaryFile,
            AppPaths.LearnedFile);

        DictionaryLoader.Instance.Load(repository);

        // F7: rank suggestions by the previous word as well as popularity.
        var ranking = new ContextRanking(
            DictionaryLoader.Instance.Bigrams,
            DictionaryLoader.Instance.Words);

        var suggestions = new SuggestionEngine(DictionaryLoader.Instance.Words, ranking);
        var services = new AppServices(repository, suggestions);

        // The user's own words go into the Trie now, so they are suggested
        // from the very first keystroke. They reach the BK tree below.
        services.Users.Load();

        // Everything learned on earlier runs is added on top of the dictionary.
        services.Learning.Load();

        services.ApplySettings();

        // Write the file out on the first run, so the user can see what the
        // options are and edit settings.txt by hand if they want to.
        services.Settings.Save();

        _current = services;
        services.StartBuildingSpellCheck();

        return services;
    }

    // Pushes the saved options into the engines that use them.
    // Safe to call more than once, because parts are built at different times.
    // Time O(1).
    public void ApplySettings()
    {
        Learning.Enabled = Settings.LearningEnabled;

        if (Autocorrect is not null)
        {
            Autocorrect.Enabled = Settings.AutocorrectEnabled;
            Autocorrect.FixMessyWords = Settings.FixMessyWords;
        }
    }

    // Fills the BK tree on a background thread, then says it is ready.
    // Time O(N * d * L * L) where N is the number of words.
    private void StartBuildingSpellCheck()
    {
        Task.Run(() =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            // GetAllWords already includes the user's own words, because
            // Users.Load put them into the same Trie.
            foreach (WordEntry entry in Dictionary.Words.GetAllWords())
            {
                Tree.Add(entry.Word);
            }

            clock.Stop();
            TreeBuildMs = clock.ElapsedMilliseconds;

            var fuzzy = new FuzzyMatcher(Tree, Dictionary.Words);

            Fuzzy = fuzzy;
            Autocorrect = new AutocorrectEngine(fuzzy, Dictionary.Words, Users);
            IsSpellCheckReady = true;

            // The typo engine is built last, so its options are applied here.
            ApplySettings();

            SpellCheckReady?.Invoke(this, EventArgs.Empty);
        });
    }
}
