using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

/// <summary>
/// The editor walks the text to find every word so it can mark the unknown
/// ones red. The walk itself is plain Core logic, so it is tested here.
/// </summary>
public class WordScannerFindWordsTests
{
    // Same walk the editor uses, kept here so the test does not need WinForms.
    private static List<string> FindWords(string text)
    {
        var words = new List<string>();
        int index = 0;

        while (index < text.Length)
        {
            if (!WordScanner.IsWordChar(text[index]))
            {
                index++;
                continue;
            }

            int start = index;
            while (index < text.Length && WordScanner.IsWordChar(text[index]))
            {
                index++;
            }

            words.Add(text.Substring(start, index - start));
        }

        return words;
    }

    [Fact]
    public void FindWords_SplitsASentenceIntoWords()
    {
        Assert.Equal(new[] { "the", "quick", "brown", "fox" }, FindWords("the quick brown fox"));
    }

    [Fact]
    public void FindWords_LeavesOutPunctuation()
    {
        // The walk keeps the original capitals. Lower casing happens later.
        Assert.Equal(new[] { "Hello", "world", "How", "are", "you" }, FindWords("Hello, world! How are you?"));
    }

    [Fact]
    public void FindWords_KeepsContractionsTogether()
    {
        Assert.Equal(new[] { "don't", "stop" }, FindWords("don't stop"));
    }

    [Fact]
    public void FindWords_SplitsAroundNumbers()
    {
        Assert.Equal(new[] { "room", "b" }, FindWords("room 404b"));
    }

    [Fact]
    public void FindWords_HandlesEmptyAndSpaceOnlyText()
    {
        Assert.Empty(FindWords(string.Empty));
        Assert.Empty(FindWords("   \n\t  "));
        Assert.Empty(FindWords("123 456"));
    }

    [Fact]
    public void FindWords_HandlesTextThatEndsMidWord()
    {
        Assert.Equal(new[] { "hello", "wor" }, FindWords("hello wor"));
    }
}
