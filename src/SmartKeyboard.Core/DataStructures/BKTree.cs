namespace SmartKeyboard.Core.DataStructures;

/// <summary>
/// A BK tree, written by hand. It finds every stored word that is close to a
/// given word, without comparing against all of them.
///
/// How it works. The first word added becomes the root. Every other word is
/// filed under its parent by its edit distance, so a child sitting on edge 2
/// is exactly 2 edits away from its parent.
///
/// The trick when searching is the triangle inequality. For any three words,
///     distance(query, child) >= | distance(query, parent) - distance(parent, child) |
/// So if we want words within maxDistance of the query, only the edges between
///     distance(query, parent) - maxDistance
/// and
///     distance(query, parent) + maxDistance
/// can possibly hold a match. Every other branch is skipped without ever
/// running edit distance on the words inside it.
/// </summary>
public class BKTree
{
    private Node? _root;

    private sealed class Node
    {
        public Node(string word)
        {
            Word = word;
        }

        public string Word { get; }

        /// <summary>Children keyed by their distance from this node's word.</summary>
        public Dictionary<int, Node> Children { get; } = new();
    }

    /// <summary>How many words are stored.</summary>
    public int Count { get; private set; }

    /// <summary>True when nothing has been added yet.</summary>
    public bool IsEmpty => _root is null;

    // Files a word into the tree by its distance from each node on the way down.
    // Time O(d * L * L) where d is the depth reached, which is small in practice.
    public void Add(string? word)
    {
        string clean = Normalize(word);
        if (clean.Length == 0)
        {
            return;
        }

        if (_root is null)
        {
            _root = new Node(clean);
            Count++;
            return;
        }

        Node current = _root;
        while (true)
        {
            int distance = EditDistance.Levenshtein(clean, current.Word);

            // The word is already in the tree.
            if (distance == 0)
            {
                return;
            }

            if (current.Children.TryGetValue(distance, out Node? child))
            {
                current = child;
                continue;
            }

            current.Children[distance] = new Node(clean);
            Count++;
            return;
        }
    }

    /// <summary>Adds many words at once.</summary>
    public void AddRange(IEnumerable<string> words)
    {
        foreach (string word in words)
        {
            Add(word);
        }
    }

    // Finds every stored word within maxDistance of the query.
    // Time is much better than checking every word, because whole branches are
    // skipped. Worst case is still O(N) if the limit is very large.
    public List<FuzzyMatch> Search(string? query, int maxDistance)
    {
        var results = new List<FuzzyMatch>();

        string clean = Normalize(query);
        if (_root is null || clean.Length == 0 || maxDistance < 0)
        {
            return results;
        }

        // A stack instead of recursion, so a deep tree can never overflow.
        var toVisit = new Stack<Node>();
        toVisit.Push(_root);

        while (toVisit.Count > 0)
        {
            Node node = toVisit.Pop();

            // The exact distance is needed either way, because the pruning
            // range below is built from it. So no early exit version here.
            int distance = EditDistance.Levenshtein(clean, node.Word);

            if (distance <= maxDistance)
            {
                results.Add(new FuzzyMatch(node.Word, distance));
            }

            // Only these edges can hold a word close enough to the query.
            int low = distance - maxDistance;
            int high = distance + maxDistance;

            foreach (KeyValuePair<int, Node> child in node.Children)
            {
                if (child.Key >= low && child.Key <= high)
                {
                    toVisit.Push(child.Value);
                }
            }
        }

        return results;
    }

    /// <summary>True when the exact word is in the tree.</summary>
    public bool Contains(string? word)
    {
        return Search(word, 0).Count > 0;
    }

    // Lower cases and trims. Time O(L).
    private static string Normalize(string? word)
    {
        return word is null ? string.Empty : word.Trim().ToLowerInvariant();
    }
}
