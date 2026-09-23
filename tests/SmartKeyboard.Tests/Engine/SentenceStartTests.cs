using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

/// <summary>
/// The editor has to know whether a word is the first of a sentence, because
/// that is the only place a capital letter carries no meaning. Everywhere else
/// a capital usually means a name, and names must never be autocorrected.
///
/// The walk itself is plain Core logic, so it is tested here without WinForms.
/// </summary>
public class SentenceStartTests
{
    // Same walk the editor uses.
    private static bool IsSentenceStart(string text, int wordStart)
    {
        for (int i = wordStart - 1; i >= 0; i--)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return WordScanner.IsSentenceEnd(c);
        }

        return true;
    }

    [Fact]
    public void TheVeryFirstWordCountsAsASentenceStart()
    {
        Assert.True(IsSentenceStart("Hello there", 0));
    }

    [Fact]
    public void AWordAfterAFullStopIsASentenceStart()
    {
        const string text = "I am done. Teh";
        Assert.True(IsSentenceStart(text, 11));
    }

    [Theory]
    [InlineData("Really? Teh", 8)]
    [InlineData("Stop! Teh", 6)]
    [InlineData("Done.   Teh", 8)]
    public void AnyEndOfSentenceMarkCounts(string text, int wordStart)
    {
        Assert.True(IsSentenceStart(text, wordStart));
    }

    [Fact]
    public void AWordInTheMiddleOfASentenceIsNotASentenceStart()
    {
        const string text = "hello Qadees";
        Assert.False(IsSentenceStart(text, 6));
    }

    [Fact]
    public void AWordAfterACommaIsNotASentenceStart()
    {
        const string text = "hello, Qadees";
        Assert.False(IsSentenceStart(text, 7));
    }

    [Fact]
    public void AWordOnANewLineAfterAFullStopIsASentenceStart()
    {
        const string text = "Done.\nTeh";
        Assert.True(IsSentenceStart(text, 6));
    }

    [Fact]
    public void AWordOnANewLineWithNoFullStopIsNot()
    {
        const string text = "done\nTeh";
        Assert.False(IsSentenceStart(text, 5));
    }
}
