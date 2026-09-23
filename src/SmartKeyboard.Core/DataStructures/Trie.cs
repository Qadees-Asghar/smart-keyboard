namespace SmartKeyboard.Core.DataStructures;

/// <summary>
/// A prefix tree. It stores words letter by letter so that finding every word
/// starting with some prefix is fast, no matter how many words are stored.
/// All words are kept in lower case so lookups are not case sensitive.
/// This class is written by hand, no library collections other than Dictionary.
/// </summary>
public class Trie
{
    private readonly TrieNode _root = new();

    /// <summary>How many distinct words are stored.</summary>
    public int WordCount { get; private set; }

    /// <summary>Sum of the frequency of every stored word. Used for P(w).</summary>
    public long TotalFrequency { get; private set; }

    /// <summary>
    /// The count of the commonest word stored. It tells the rest of the
    /// program what scale this dictionary uses, so things like the learning
    /// weight work whichever word list is loaded.
    /// </summary>
    public int MaxFrequency { get; private set; }

    // Adds a word, or raises the frequency of a word that is already there.
    // Time O(L) where L is the length of the word. Space O(L) worst case.
    public void Insert(string? word, int frequency = 1)
    {
        string clean = Normalize(word);
        if (clean.Length == 0)
        {
            return;
        }

        TrieNode node = _root;
        foreach (char letter in clean)
        {
            if (!node.Children.TryGetValue(letter, out TrieNode? next))
            {
                next = new TrieNode();
                node.Children[letter] = next;
            }

            node = next;
        }

        if (!node.IsWord)
        {
            node.IsWord = true;
            WordCount++;
        }

        // A repeated insert adds to the count instead of replacing it.
        node.Frequency += frequency;
        TotalFrequency += frequency;

        if (node.Frequency > MaxFrequency)
        {
            MaxFrequency = node.Frequency;
        }
    }

    // Sets the frequency of a word to an exact value, adding the word if needed.
    // Used when merging learned counts on top of the dictionary.
    // Time O(L).
    public void SetFrequency(string? word, int frequency)
    {
        string clean = Normalize(word);
        if (clean.Length == 0)
        {
            return;
        }

        Insert(clean, 0);
        TrieNode? node = FindNode(clean);
        if (node is null)
        {
            return;
        }

        TotalFrequency += frequency - node.Frequency;
        node.Frequency = frequency;

        if (frequency > MaxFrequency)
        {
            MaxFrequency = frequency;
        }
    }

    // True when the exact word is stored. This is the spell check lookup.
    // Time O(L).
    public bool Contains(string? word)
    {
        TrieNode? node = FindNode(Normalize(word));
        return node is not null && node.IsWord;
    }

    // Gives back the frequency of a stored word, or 0 when the word is unknown.
    // Time O(L).
    public int GetFrequency(string? word)
    {
        TrieNode? node = FindNode(Normalize(word));
        return node is not null && node.IsWord ? node.Frequency : 0;
    }

    // True when at least one stored word starts with this prefix.
    // Time O(L).
    public bool StartsWith(string? prefix)
    {
        return FindNode(Normalize(prefix)) is not null;
    }

    // Collects every word that starts with the prefix.
    // Time O(L + M) where L is the prefix length and M is the total number of
    // nodes under the prefix. Space O(M) for the results.
    public List<WordEntry> GetWordsWithPrefix(string? prefix)
    {
        string clean = Normalize(prefix);
        var results = new List<WordEntry>();

        TrieNode? start = FindNode(clean);
        if (start is null)
        {
            return results;
        }

        Collect(start, clean, results);
        return results;
    }

    // Collects every word in the whole Trie.
    // Time O(N) over all nodes. Space O(number of words).
    public List<WordEntry> GetAllWords()
    {
        var results = new List<WordEntry>();
        Collect(_root, string.Empty, results);
        return results;
    }

    // Removes a word and cleans up any nodes that are no longer needed.
    // Time O(L). Returns false when the word was not there.
    public bool Remove(string? word)
    {
        string clean = Normalize(word);
        if (clean.Length == 0 || !Contains(clean))
        {
            return false;
        }

        RemoveFrom(_root, clean, 0);
        WordCount--;
        return true;
    }

    // Walks down from a node collecting words. Depth first search.
    // Time O(number of nodes visited).
    private static void Collect(TrieNode node, string sofar, List<WordEntry> results)
    {
        if (node.IsWord)
        {
            results.Add(new WordEntry(sofar, node.Frequency));
        }

        foreach (KeyValuePair<char, TrieNode> child in node.Children)
        {
            Collect(child.Value, sofar + child.Key, results);
        }
    }

    // Walks down the tree following the letters of the prefix.
    // Returns null as soon as a letter is missing. Time O(L).
    private TrieNode? FindNode(string prefix)
    {
        TrieNode node = _root;
        foreach (char letter in prefix)
        {
            if (!node.Children.TryGetValue(letter, out TrieNode? next))
            {
                return null;
            }

            node = next;
        }

        return node;
    }

    // Deletes the word mark, then drops nodes on the way back up if they are
    // not used by any other word. Returns true when the caller may drop this node.
    // Time O(L).
    private bool RemoveFrom(TrieNode node, string word, int index)
    {
        if (index == word.Length)
        {
            TotalFrequency -= node.Frequency;
            node.IsWord = false;
            node.Frequency = 0;
            return node.Children.Count == 0;
        }

        char letter = word[index];
        if (!node.Children.TryGetValue(letter, out TrieNode? child))
        {
            return false;
        }

        if (RemoveFrom(child, word, index + 1))
        {
            node.Children.Remove(letter);
        }

        return node.Children.Count == 0 && !node.IsWord;
    }

    // Lower cases the text and trims spaces. Null becomes an empty string.
    // Time O(L).
    private static string Normalize(string? word)
    {
        return word is null ? string.Empty : word.Trim().ToLowerInvariant();
    }
}
