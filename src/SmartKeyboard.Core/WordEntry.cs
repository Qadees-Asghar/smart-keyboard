namespace SmartKeyboard.Core;

/// <summary>
/// A word plus how often it is used. This is the small piece of data
/// that moves between the Trie, the heap, and the engines.
/// </summary>
public sealed class WordEntry
{
    public WordEntry(string word, int frequency)
    {
        Word = word;
        Frequency = frequency;
    }

    public string Word { get; }

    public int Frequency { get; }

    public override string ToString() => $"{Word} ({Frequency})";
}
