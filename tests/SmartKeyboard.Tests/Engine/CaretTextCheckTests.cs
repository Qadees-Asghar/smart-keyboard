using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class CaretTextCheckTests
{
    // The text before the caret decides whether the first word after a click
    // may be completed or corrected. Every "no" below is a case where saying
    // yes would have let SmartKeyboard wreck a word already on screen.

    [Theory]
    [InlineData("say helo ", "helo ")]
    [InlineData("line one\nhelo ", "helo ")]
    [InlineData("(helo ", "helo ")]
    [InlineData("done.helo ", "helo ")]
    [InlineData("\"helo,", "helo,")]
    public void AWordWithABoundaryInFrontIsWhole(string before, string expected)
    {
        string tail = before.Length > expected.Length + 1 ? before[^(expected.Length + 1)..] : before;

        Assert.True(CaretTextCheck.ConfirmsWord(tail, expected, expected.Length + 1));
    }

    [Fact]
    public void AWordAtTheVeryStartOfTheTextIsWhole()
    {
        // Asked for six, got five: the text begins with the word.
        Assert.True(CaretTextCheck.ConfirmsWord("helo ", "helo ", 6));
    }

    [Fact]
    public void GettingBackExactlyWhatWasAskedForIsNotTheStartOfTheText()
    {
        // Asked for five and got five. Something may sit in front of it.
        Assert.False(CaretTextCheck.ConfirmsWord("helo ", "helo ", 5));
    }

    [Fact]
    public void TheTailOfALongerWordIsNotWhole()
    {
        // Clicked after "wor" and typed "ld ". Fixing "ld" would wreck "world".
        Assert.False(CaretTextCheck.ConfirmsWord("rld ", "ld ", 4));
    }

    [Fact]
    public void ADigitInFrontMeansTheWordIsNotWhole()
    {
        Assert.False(CaretTextCheck.ConfirmsWord("4helo ", "helo ", 6));
    }

    [Theory]
    [InlineData("xhelo ")]
    [InlineData(" help ")]
    [InlineData(" helo")]
    [InlineData("")]
    public void TextThatDoesNotEndWithTheWordIsRejected(string before)
    {
        Assert.False(CaretTextCheck.ConfirmsWord(before, "helo ", 6));
    }

    [Fact]
    public void AnAppThatWouldNotSayIsANo()
    {
        Assert.False(CaretTextCheck.ConfirmsWord(null, "helo ", 6));
        Assert.False(CaretTextCheck.ConfirmsPrefix(null, "hel"));
    }

    [Fact]
    public void APrefixIsConfirmedWhenEveryLetterHasLanded()
    {
        Assert.True(CaretTextCheck.ConfirmsPrefix(" hel", "hel"));
    }

    [Fact]
    public void APrefixIsConfirmedWhenTheLastLetterIsStillOnItsWay()
    {
        // The hook sees a key before the app does, so "l" may not be there yet.
        Assert.True(CaretTextCheck.ConfirmsPrefix("a he", "hel"));
    }

    [Fact]
    public void APrefixAtTheStartOfTheTextIsConfirmed()
    {
        Assert.True(CaretTextCheck.ConfirmsPrefix("hel", "hel"));
        Assert.True(CaretTextCheck.ConfirmsPrefix("he", "hel"));
    }

    [Fact]
    public void APrefixThatContinuesAWordIsRejected()
    {
        // "wor" on screen, the user clicked after it and typed "ld".
        Assert.False(CaretTextCheck.ConfirmsPrefix("orl", "ld"));
        Assert.False(CaretTextCheck.ConfirmsPrefix("wor", "ld"));
    }
}
