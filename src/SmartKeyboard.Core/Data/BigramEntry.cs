namespace SmartKeyboard.Core.Data;

/// <summary>
/// One line of bigrams.txt: two words that appeared next to each other,
/// and how many times that happened.
/// </summary>
public sealed class BigramEntry
{
    public BigramEntry(string first, string second, int count)
    {
        First = first;
        Second = second;
        Count = count;
    }

    public string First { get; }

    public string Second { get; }

    public int Count { get; }

    public override string ToString() => $"{First} {Second} ({Count})";
}
