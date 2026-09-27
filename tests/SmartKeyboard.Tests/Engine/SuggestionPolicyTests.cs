using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class SuggestionPolicyTests
{
    [Fact]
    public void OneLetterIsNotEnoughToShowAnything()
    {
        // The old rule, and the reason the box felt like it was always up.
        // One letter matches thousands of words, so the list said nothing.
        Assert.False(SuggestionPolicy.ShouldShowCompletions("h", targetTakesText: true, wordStartKnown: true));
    }

    [Fact]
    public void TwoLettersIsEnough()
    {
        Assert.True(SuggestionPolicy.ShouldShowCompletions("he", targetTakesText: true, wordStartKnown: true));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void NothingTypedShowsNothing(string? prefix)
    {
        Assert.False(SuggestionPolicy.ShouldShowCompletions(prefix, targetTakesText: true, wordStartKnown: true));
    }

    [Fact]
    public void NoTextTargetMeansNoCompletionsHoweverMuchIsTyped()
    {
        // Letters pressed with the desktop or a file list in focus.
        Assert.False(SuggestionPolicy.ShouldShowCompletions("hello", targetTakesText: false, wordStartKnown: true));
    }

    [Fact]
    public void APredictionNeedsATypingSessionAlreadyRunning()
    {
        // Pressing space once on an idle screen must not put a list of
        // guessed next words up.
        Assert.False(SuggestionPolicy.ShouldShowPredictions(targetTakesText: true, sessionLive: false));
    }

    [Fact]
    public void APredictionIsFineInTheMiddleOfWriting()
    {
        // Finishing a word mid sentence counts as typing, so the next word
        // guess still works. This is the case that must not be lost while
        // making the box quieter.
        Assert.True(SuggestionPolicy.ShouldShowPredictions(targetTakesText: true, sessionLive: true));
    }

    [Fact]
    public void NoTextTargetMeansNoPredictionEither()
    {
        Assert.False(SuggestionPolicy.ShouldShowPredictions(targetTakesText: false, sessionLive: true));
    }

    [Fact]
    public void TheReasonForRefusingIsReported()
    {
        // The diagnostics log prints these, so they have to be right.
        Assert.Equal(
            SuggestionPolicy.Refusal.NotATextTarget,
            SuggestionPolicy.CheckCompletions("hello", targetTakesText: false, wordStartKnown: true));

        Assert.Equal(
            SuggestionPolicy.Refusal.TooFewLetters,
            SuggestionPolicy.CheckCompletions("h", targetTakesText: true, wordStartKnown: true));

        Assert.Equal(
            SuggestionPolicy.Refusal.NoSession,
            SuggestionPolicy.CheckPredictions(targetTakesText: true, sessionLive: false));

        Assert.Equal(
            SuggestionPolicy.Refusal.None,
            SuggestionPolicy.CheckCompletions("he", targetTakesText: true, wordStartKnown: true));
    }

    [Fact]
    public void NotATextTargetBeatsTooFewLetters()
    {
        // When both are wrong the more useful reason is reported, because
        // that is the one worth acting on.
        Assert.Equal(
            SuggestionPolicy.Refusal.NotATextTarget,
            SuggestionPolicy.CheckCompletions("h", targetTakesText: false, wordStartKnown: true));
    }

    [Fact]
    public void AWordWeDidNotWatchFromTheStartIsNeverCompleted()
    {
        // The whole point. After a click or an arrow key the letters counted
        // here may be only the tail of the word on screen, so replacing them
        // would delete too few characters and leave "hehello" behind.
        Assert.False(SuggestionPolicy.ShouldShowCompletions(
            "llo", targetTakesText: true, wordStartKnown: false));
    }

    [Fact]
    public void NotKnowingTheStartIsReportedAsItsOwnReason()
    {
        Assert.Equal(
            SuggestionPolicy.Refusal.WordStartUnknown,
            SuggestionPolicy.CheckCompletions("llo", targetTakesText: true, wordStartKnown: false));
    }

    [Fact]
    public void NotATextTargetStillBeatsNotKnowingTheStart()
    {
        // Both are true here, and the more useful reason is the one reported.
        Assert.Equal(
            SuggestionPolicy.Refusal.NotATextTarget,
            SuggestionPolicy.CheckCompletions("llo", targetTakesText: false, wordStartKnown: false));
    }

    [Fact]
    public void PlentyOfLettersDoesNotMakeAnUnknownStartSafe()
    {
        // Length is no evidence at all: a long tail of a longer word is
        // exactly the dangerous case.
        Assert.False(SuggestionPolicy.ShouldShowCompletions(
            "ellowworld", targetTakesText: true, wordStartKnown: false));
    }
}
