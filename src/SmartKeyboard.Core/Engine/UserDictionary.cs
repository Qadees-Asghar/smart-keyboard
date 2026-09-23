using SmartKeyboard.Core.Data;
using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Core.Engine;

/// <summary>
/// The words the user added by hand: names, slang, subject words, anything
/// the shipped dictionary does not know.
///
/// The words live in a HashSet, so checking one is O(1) on average.
/// A new word also goes straight into the Trie and the BK tree, so it can be
/// suggested and offered as a fix immediately, with no restart.
///
/// The list is saved to user_dict.txt through the repository.
/// </summary>
public class UserDictionary
{
    /// <summary>
    /// The count given to a word the user adds. It is high enough that the
    /// word shows up in suggestions, but far below the really common words.
    /// </summary>
    public const int DefaultFrequency = 100;

    private readonly HashSet<string> _words = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWordRepository _repository;
    private readonly Trie _trie;
    private readonly BKTree? _tree;

    public UserDictionary(IWordRepository repository, Trie trie, BKTree? tree = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _trie = trie ?? throw new ArgumentNullException(nameof(trie));
        _tree = tree;
    }

    /// <summary>How many words the user has added.</summary>
    public int Count => _words.Count;

    // Reads the saved words and puts them into the Trie and the BK tree.
    // Time O(N * L) where N is the number of saved words.
    public void Load()
    {
        _words.Clear();

        foreach (string word in _repository.LoadUserWords())
        {
            string clean = Normalize(word);
            if (clean.Length == 0)
            {
                continue;
            }

            _words.Add(clean);
            Teach(clean);
        }
    }

    // Adds a word and saves the list. Returns false when the word was already
    // there or is not a real word.
    // Time O(L) for the set and the Trie, plus the BK tree add.
    public bool Add(string? word)
    {
        string clean = Normalize(word);
        if (!IsAllowed(clean) || !_words.Add(clean))
        {
            return false;
        }

        Teach(clean);
        Save();
        return true;
    }

    // Removes a word and saves the list.
    // The word also comes out of the Trie, which is what spell check reads,
    // so it goes back to being marked as unknown straight away.
    // Time O(L).
    public bool Remove(string? word)
    {
        string clean = Normalize(word);
        if (!_words.Remove(clean))
        {
            return false;
        }

        _trie.Remove(clean);
        Save();
        return true;
    }

    /// <summary>True when the user added this word.</summary>
    // Time O(1) on average.
    public bool Contains(string? word)
    {
        return _words.Contains(Normalize(word));
    }

    /// <summary>Every word the user added, in alphabetical order.</summary>
    // Time O(N log N) for the sort.
    public List<string> GetAll()
    {
        return _words.OrderBy(w => w, StringComparer.Ordinal).ToList();
    }

    // Removes every word. Used by the "clear list" button.
    // Time O(N * L).
    public void Clear()
    {
        foreach (string word in _words)
        {
            _trie.Remove(word);
        }

        _words.Clear();
        Save();
    }

    /// <summary>Writes the list to user_dict.txt.</summary>
    // Time O(N).
    public void Save()
    {
        _repository.SaveUserWords(GetAll());
    }

    // Checks a word is worth storing. Digits and punctuation are refused,
    // because they are not words the spell checker can help with.
    // Time O(L).
    public static bool IsAllowed(string? word)
    {
        string clean = Normalize(word);
        return clean.Length > 0 && clean.All(WordScanner.IsWordChar);
    }

    // Puts a word into the Trie and the BK tree so it works right away.
    // Time O(L) plus the BK tree add.
    private void Teach(string word)
    {
        if (!_trie.Contains(word))
        {
            _trie.Insert(word, DefaultFrequency);
        }

        _tree?.Add(word);
    }

    // Lower cases and trims. Time O(L).
    private static string Normalize(string? word)
    {
        return word is null ? string.Empty : word.Trim().ToLowerInvariant();
    }
}
