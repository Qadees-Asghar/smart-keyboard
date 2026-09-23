namespace SmartKeyboard.Core.Data;

/// <summary>
/// Reads and writes the word data as plain text files.
///
/// words.txt     one line per word:  "word count"
/// bigrams.txt   one line per pair:  "word1 word2 count"
/// user_dict.txt one word per line
/// learned.txt   "W word count" for a word, "P word1 word2 count" for a pair
///
/// Lines that are blank or do not fit the format are skipped quietly, so one
/// bad line never stops the program from starting.
/// </summary>
public class FileWordRepository : IWordRepository
{
    private static readonly char[] Separators = { ' ', '\t' };

    private readonly string _wordsPath;
    private readonly string _bigramsPath;
    private readonly string _userDictPath;
    private readonly string _learnedPath;

    public FileWordRepository(string dataFolder)
        : this(
            Path.Combine(dataFolder, "words.txt"),
            Path.Combine(dataFolder, "bigrams.txt"),
            Path.Combine(dataFolder, "user_dict.txt"),
            Path.Combine(dataFolder, "learned.txt"))
    {
    }

    public FileWordRepository(string wordsPath, string bigramsPath, string userDictPath, string learnedPath)
    {
        _wordsPath = wordsPath;
        _bigramsPath = bigramsPath;
        _userDictPath = userDictPath;
        _learnedPath = learnedPath;
    }

    /// <summary>Where the user's own words are saved.</summary>
    public string UserDictionaryPath => _userDictPath;

    /// <summary>Where the learned counts are saved.</summary>
    public string LearnedPath => _learnedPath;

    // Reads words.txt. Time O(N) over the lines in the file.
    // The missing file check happens straight away, not when reading starts.
    public IEnumerable<WordEntry> LoadWords()
    {
        RequireFile(_wordsPath, "words.txt");
        return ReadWords();
    }

    private IEnumerable<WordEntry> ReadWords()
    {
        foreach (string line in File.ReadLines(_wordsPath))
        {
            string[] parts = Split(line, 2);
            if (parts.Length < 1)
            {
                continue;
            }

            string word = parts[0].ToLowerInvariant();
            int count = parts.Length > 1 && int.TryParse(parts[1], out int parsed) ? parsed : 1;

            if (word.Length > 0 && count > 0)
            {
                yield return new WordEntry(word, count);
            }
        }
    }

    // Reads bigrams.txt. Time O(N) over the lines in the file.
    // The missing file check happens straight away, not when reading starts.
    public IEnumerable<BigramEntry> LoadBigrams()
    {
        RequireFile(_bigramsPath, "bigrams.txt");
        return ReadBigrams();
    }

    private IEnumerable<BigramEntry> ReadBigrams()
    {
        foreach (string line in File.ReadLines(_bigramsPath))
        {
            string[] parts = Split(line, 3);
            if (parts.Length < 2)
            {
                continue;
            }

            string first = parts[0].ToLowerInvariant();
            string second = parts[1].ToLowerInvariant();
            int count = parts.Length > 2 && int.TryParse(parts[2], out int parsed) ? parsed : 1;

            if (first.Length > 0 && second.Length > 0 && count > 0)
            {
                yield return new BigramEntry(first, second, count);
            }
        }
    }

    // Reads user_dict.txt. A missing file just means the user added nothing yet.
    // Time O(N).
    public IEnumerable<string> LoadUserWords()
    {
        return File.Exists(_userDictPath) ? ReadUserWords() : Enumerable.Empty<string>();
    }

    private IEnumerable<string> ReadUserWords()
    {
        foreach (string line in File.ReadLines(_userDictPath))
        {
            string word = line.Trim().ToLowerInvariant();
            if (word.Length > 0)
            {
                yield return word;
            }
        }
    }

    // Writes user_dict.txt. Time O(N).
    public void SaveUserWords(IEnumerable<string> words)
    {
        EnsureFolder(_userDictPath);
        File.WriteAllLines(_userDictPath, words.Select(w => w.Trim().ToLowerInvariant()).Where(w => w.Length > 0));
    }

    // Reads learned.txt. A missing file just means nothing has been learned yet.
    // Time O(N).
    public LearnedCounts LoadLearned()
    {
        var counts = new LearnedCounts();
        if (!File.Exists(_learnedPath))
        {
            return counts;
        }

        foreach (string line in File.ReadLines(_learnedPath))
        {
            string[] parts = Split(line, 4);
            if (parts.Length < 3)
            {
                continue;
            }

            if (parts[0] == "W" && parts.Length >= 3 && int.TryParse(parts[2], out int wordCount))
            {
                counts.Words[parts[1].ToLowerInvariant()] = wordCount;
            }
            else if (parts[0] == "P" && parts.Length >= 4 && int.TryParse(parts[3], out int pairCount))
            {
                counts.Pairs[$"{parts[1].ToLowerInvariant()} {parts[2].ToLowerInvariant()}"] = pairCount;
            }
        }

        return counts;
    }

    // Writes learned.txt. Time O(N).
    public void SaveLearned(LearnedCounts counts)
    {
        EnsureFolder(_learnedPath);

        var lines = new List<string>();
        foreach (KeyValuePair<string, int> word in counts.Words)
        {
            lines.Add($"W {word.Key} {word.Value}");
        }

        foreach (KeyValuePair<string, int> pair in counts.Pairs)
        {
            lines.Add($"P {pair.Key} {pair.Value}");
        }

        File.WriteAllLines(_learnedPath, lines);
    }

    // Splits a line on spaces or tabs, keeping at most "max" pieces.
    // Time O(L).
    private static string[] Split(string line, int max)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return Array.Empty<string>();
        }

        return trimmed.Split(Separators, max, StringSplitOptions.RemoveEmptyEntries);
    }

    // Stops with a clear message when a required file is missing.
    private static void RequireFile(string path, string friendlyName)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The dictionary file {friendlyName} was not found. Expected it at: {path}",
                path);
        }
    }

    // Creates the folder for a file we are about to write, if needed.
    private static void EnsureFolder(string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }
}
