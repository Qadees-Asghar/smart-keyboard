using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class WordScannerTests
{
    [Theory]
    [InlineData("hello wor", 9, "wor")]
    [InlineData("hello", 5, "hello")]
    [InlineData("hello ", 6, "")]
    [InlineData("don't", 5, "don't")]
    [InlineData("", 0, "")]
    public void GetCurrentPrefix_ReadsTheWordBeingTyped(string text, int caret, string expected)
    {
        Assert.Equal(expected, WordScanner.GetCurrentPrefix(text, caret));
    }

    [Fact]
    public void GetCurrentPrefix_StopsAtTheCaretNotTheEndOfTheWord()
    {
        // Caret sits between "he" and "llo".
        Assert.Equal("he", WordScanner.GetCurrentPrefix("hello", 2));
    }

    [Fact]
    public void GetCurrentPrefix_HandlesACaretOutsideTheText()
    {
        Assert.Equal("hello", WordScanner.GetCurrentPrefix("hello", 999));
        Assert.Equal(string.Empty, WordScanner.GetCurrentPrefix("hello", -5));
    }

    [Fact]
    public void GetCurrentWordStart_PointsAtTheFirstLetterOfTheWord()
    {
        Assert.Equal(6, WordScanner.GetCurrentWordStart("hello world", 9));
        Assert.Equal(0, WordScanner.GetCurrentWordStart("hello", 3));
    }

    [Theory]
    [InlineData("good mor", "good")]
    [InlineData("the quick brown fo", "brown")]
    [InlineData("hello", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void GetPreviousWord_FindsTheFinishedWordBeforeTheOneBeingTyped(string text, string? expected)
    {
        Assert.Equal(expected, WordScanner.GetPreviousWord(text, text.Length));
    }

    [Fact]
    public void GetPreviousWord_LowerCasesWhatItFinds()
    {
        Assert.Equal("good", WordScanner.GetPreviousWord("Good mor", 8));
    }

    [Fact]
    public void GetPreviousWord_IsNullAfterAFullStop()
    {
        Assert.Null(WordScanner.GetPreviousWord("I am done. Th", 13));
        Assert.Null(WordScanner.GetPreviousWord("Really? Th", 10));
        Assert.Null(WordScanner.GetPreviousWord("Stop! Th", 8));
    }

    [Fact]
    public void GetPreviousWord_SkipsCommasAndOtherPunctuation()
    {
        Assert.Equal("hello", WordScanner.GetPreviousWord("hello, th", 9));
    }

    [Fact]
    public void GetPreviousWord_WorksWhenNothingIsTypedYet()
    {
        Assert.Equal("good", WordScanner.GetPreviousWord("good ", 5));
    }

    [Theory]
    [InlineData('a', true)]
    [InlineData('Z', true)]
    [InlineData('\'', true)]
    [InlineData(' ', false)]
    [InlineData('.', false)]
    [InlineData('5', false)]
    public void IsWordChar_KnowsWhichCharactersBuildAWord(char c, bool expected)
    {
        Assert.Equal(expected, WordScanner.IsWordChar(c));
    }

    [Theory]
    [InlineData(' ', true)]
    [InlineData('.', true)]
    [InlineData(',', true)]
    [InlineData('!', true)]
    [InlineData(';', true)]
    [InlineData('a', false)]
    [InlineData('5', false)]
    public void IsWordSeparator_KnowsWhenAWordIsFinished(char c, bool expected)
    {
        Assert.Equal(expected, WordScanner.IsWordSeparator(c));
    }

    [Theory]
    [InlineData((char)8)]    // Backspace
    [InlineData((char)13)]   // Enter
    [InlineData((char)10)]   // Line feed
    [InlineData((char)9)]    // Tab
    [InlineData((char)27)]   // Escape
    public void IsWordSeparator_NeverTreatsAKeyPressAsAFinishedWord(char key)
    {
        // Backspace used to count as finishing a word, so autocorrect fired
        // the moment you pressed it to delete a letter and fix a typo by hand.
        Assert.False(WordScanner.IsWordSeparator(key));
    }

    [Theory]
    [InlineData("teh", "the", "the")]
    [InlineData("Teh", "the", "The")]
    [InlineData("TEH", "the", "THE")]
    [InlineData("tEh", "the", "the")]
    [InlineData("", "the", "the")]
    public void MatchCapitalization_CopiesTheShapeOfTheOriginalWord(string original, string replacement, string expected)
    {
        Assert.Equal(expected, WordScanner.MatchCapitalization(original, replacement));
    }

    [Fact]
    public void MatchCapitalization_DoesNotShoutForASingleLetter()
    {
        Assert.Equal("A", WordScanner.MatchCapitalization("A", "a"));
    }
}
