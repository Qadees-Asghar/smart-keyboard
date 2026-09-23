namespace SmartKeyboard.Core.DataStructures;

/// <summary>
/// Stores how often one word is followed by another.
/// It is a hash map of hash maps: first word -> second word -> count.
/// A .NET Dictionary is allowed here because the project only requires the
/// Trie, the heap, the BK tree, and edit distance to be written by hand.
///
/// Looking up "which words usually come after 'good'" is O(1) plus the number
/// of words that actually follow it.
/// </summary>
public class BigramIndex
{
    private readonly Dictionary<string, Dictionary<string, int>> _followers = new();

    // Total count of every pair starting with a given word. Keeping it here
    // saves adding the counts up every time we need P(next | prev).
    private readonly Dictionary<string, int> _totalAfter = new();

    /// <summary>How many different first words are stored.</summary>
    public int FirstWordCount => _followers.Count;

    /// <summary>How many word pairs are stored.</summary>
    public int PairCount { get; private set; }

    // Records that "second" follows "first" this many times.
    // Calling it again adds to the count. Time O(1) on average.
    public void Add(string? first, string? second, int count = 1)
    {
        string a = Normalize(first);
        string b = Normalize(second);
        if (a.Length == 0 || b.Length == 0)
        {
            return;
        }

        if (!_followers.TryGetValue(a, out Dictionary<string, int>? map))
        {
            map = new Dictionary<string, int>();
            _followers[a] = map;
        }

        if (map.TryGetValue(b, out int existing))
        {
            map[b] = existing + count;
        }
        else
        {
            map[b] = count;
            PairCount++;
        }

        _totalAfter[a] = GetTotalAfter(a) + count;
    }

    // How many times "second" followed "first". Time O(1) on average.
    public int GetCount(string? first, string? second)
    {
        string a = Normalize(first);
        string b = Normalize(second);

        if (_followers.TryGetValue(a, out Dictionary<string, int>? map) &&
            map.TryGetValue(b, out int count))
        {
            return count;
        }

        return 0;
    }

    // How many word pairs in total start with this word.
    // This is count(prev), the bottom of the P(next | prev) division.
    // Time O(1) on average.
    public int GetTotalAfter(string? first)
    {
        return _totalAfter.TryGetValue(Normalize(first), out int total) ? total : 0;
    }

    // The chance that "second" comes next, given that "first" was just typed.
    // P(next | prev) = count(prev, next) / count(prev). Returns 0 when unknown.
    // Time O(1) on average.
    public double GetProbability(string? first, string? second)
    {
        int total = GetTotalAfter(first);
        return total == 0 ? 0.0 : (double)GetCount(first, second) / total;
    }

    // Every word that has been seen after this word, with its count.
    // Time O(F) where F is the number of different followers.
    public List<WordEntry> GetFollowers(string? first)
    {
        var results = new List<WordEntry>();
        if (!_followers.TryGetValue(Normalize(first), out Dictionary<string, int>? map))
        {
            return results;
        }

        foreach (KeyValuePair<string, int> pair in map)
        {
            results.Add(new WordEntry(pair.Key, pair.Value));
        }

        return results;
    }

    /// <summary>True when we have ever seen anything follow this word.</summary>
    public bool HasFollowers(string? first) => GetTotalAfter(first) > 0;

    // Lower cases and trims. Time O(L).
    private static string Normalize(string? word)
    {
        return word is null ? string.Empty : word.Trim().ToLowerInvariant();
    }
}
