namespace SmartKeyboard.Core.DataStructures;

/// <summary>
/// One node of the Trie. Each node stands for one letter.
/// The letter itself is the key in the parent's Children map.
/// </summary>
public class TrieNode
{
    /// <summary>Child nodes, keyed by the next letter.</summary>
    public Dictionary<char, TrieNode> Children { get; } = new();

    /// <summary>True when a complete word ends at this node.</summary>
    public bool IsWord { get; set; }

    /// <summary>
    /// How often this word is used. Only meaningful when IsWord is true.
    /// Higher number means the word is more common.
    /// </summary>
    public int Frequency { get; set; }
}
