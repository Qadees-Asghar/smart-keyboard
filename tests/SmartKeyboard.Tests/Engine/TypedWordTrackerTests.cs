using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class TypedWordTrackerTests
{
    // Types a whole string through the tracker, one key at a time.
    private static TypedWordTracker Type(string keys)
    {
        var tracker = new TypedWordTracker();
        foreach (char c in keys)
        {
            tracker.AddCharacter(c);
        }

        return tracker;
    }

    [Fact]
    public void LettersBuildUpTheCurrentWord()
    {
        Assert.Equal("hello", Type("hello").CurrentWord);
    }

    [Fact]
    public void ASpaceFinishesTheWordAndRemembersIt()
    {
        TypedWordTracker tracker = Type("hello ");

        Assert.Equal(string.Empty, tracker.CurrentWord);
        Assert.Equal("hello", tracker.PreviousWord);
    }

    [Fact]
    public void TheWordBeforeIsKeptWhileTheNextOneIsTyped()
    {
        TypedWordTracker tracker = Type("good mor");

        Assert.Equal("mor", tracker.CurrentWord);
        Assert.Equal("good", tracker.PreviousWord);
    }

    [Fact]
    public void ThePreviousWordIsAlwaysLowerCase()
    {
        Assert.Equal("good", Type("Good ").PreviousWord);
        Assert.Equal("good", Type("GOOD ").PreviousWord);
    }

    [Fact]
    public void AnApostropheStaysInsideTheWord()
    {
        Assert.Equal("don't", Type("don't").CurrentWord);
    }

    [Theory]
    [InlineData("hello. ")]
    [InlineData("hello? ")]
    [InlineData("hello! ")]
    public void AFullStopClearsTheContext(string keys)
    {
        TypedWordTracker tracker = Type(keys);

        Assert.Null(tracker.PreviousWord);
        Assert.Equal(string.Empty, tracker.CurrentWord);
    }

    [Fact]
    public void ACommaFinishesTheWordButKeepsTheContext()
    {
        TypedWordTracker tracker = Type("hello, ");

        Assert.Equal("hello", tracker.PreviousWord);
    }

    [Fact]
    public void ADigitThrowsAwayTheWordBecauseItIsACodeNotAWord()
    {
        TypedWordTracker tracker = Type("abc1");

        Assert.Equal(string.Empty, tracker.CurrentWord);
    }

    [Fact]
    public void Backspace_RemovesTheLastLetter()
    {
        var tracker = Type("hello");

        Assert.True(tracker.Backspace());
        Assert.Equal("hell", tracker.CurrentWord);
    }

    [Fact]
    public void Backspace_OnAnEmptyWordSaysWeHaveLostTrack()
    {
        var tracker = Type("hello ");

        // Nothing is being typed, so this backspace is eating text we cannot
        // see. The safe thing is to forget everything.
        Assert.False(tracker.Backspace());
        Assert.Null(tracker.PreviousWord);
    }

    [Fact]
    public void Backspace_AllTheWayLeavesNothing()
    {
        var tracker = Type("abc");

        Assert.True(tracker.Backspace());
        Assert.True(tracker.Backspace());
        Assert.True(tracker.Backspace());
        Assert.True(tracker.IsEmpty);
        Assert.False(tracker.Backspace());
    }

    [Fact]
    public void Reset_ForgetsEverything()
    {
        var tracker = Type("good mor");

        tracker.Reset();

        Assert.Equal(string.Empty, tracker.CurrentWord);
        Assert.Null(tracker.PreviousWord);
    }

    [Fact]
    public void Clear_ForgetsTheWordBeingTypedButKeepsTheContext()
    {
        var tracker = Type("good mor");

        tracker.Clear();

        Assert.Equal(string.Empty, tracker.CurrentWord);
        Assert.Equal("good", tracker.PreviousWord);
    }

    [Fact]
    public void AcceptWord_MakesTheAcceptedWordTheNewContext()
    {
        var tracker = Type("good mor");

        tracker.AcceptWord("morning");

        Assert.Equal(string.Empty, tracker.CurrentWord);
        Assert.Equal("morning", tracker.PreviousWord);
    }

    [Fact]
    public void WordFinished_IsRaisedOnceWithTheRightDetails()
    {
        var tracker = new TypedWordTracker();
        var finished = new List<WordFinishedEventArgs>();
        tracker.WordFinished += (_, e) => finished.Add(e);

        foreach (char c in "good morning ")
        {
            tracker.AddCharacter(c);
        }

        Assert.Equal(2, finished.Count);
        Assert.Equal("good", finished[0].Word);
        Assert.Null(finished[0].PreviousWord);
        Assert.Equal("morning", finished[1].Word);
        Assert.Equal("good", finished[1].PreviousWord);
        Assert.Equal(' ', finished[1].Separator);
    }

    [Fact]
    public void WordFinished_IsNotRaisedForTwoSpacesInARow()
    {
        var tracker = new TypedWordTracker();
        int raised = 0;
        tracker.WordFinished += (_, _) => raised++;

        foreach (char c in "hi   ")
        {
            tracker.AddCharacter(c);
        }

        Assert.Equal(1, raised);
    }

    [Fact]
    public void CurrentWordChanged_IsRaisedOnEveryLetter()
    {
        var tracker = new TypedWordTracker();
        int raised = 0;
        tracker.CurrentWordChanged += (_, _) => raised++;

        tracker.AddCharacter('a');
        tracker.AddCharacter('b');

        Assert.Equal(2, raised);
    }

    [Fact]
    public void ARealSentenceIsFollowedCorrectlyFromStartToFinish()
    {
        var tracker = new TypedWordTracker();
        var finished = new List<string>();
        tracker.WordFinished += (_, e) => finished.Add(e.Word);

        foreach (char c in "Good morning. How are you?")
        {
            tracker.AddCharacter(c);
        }

        Assert.Equal(new[] { "Good", "morning", "How", "are", "you" }, finished);

        // The sentence ended, so there is no context left.
        Assert.Null(tracker.PreviousWord);
    }
}
