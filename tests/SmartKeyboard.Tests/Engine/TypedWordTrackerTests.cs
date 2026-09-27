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

    // The version counter. It exists so a correction that was worked out a
    // moment ago can tell whether the text has moved on since, instead of
    // deleting letters that now belong to something else.

    [Fact]
    public void VersionRisesWhenALetterIsTyped()
    {
        var tracker = new TypedWordTracker();
        int before = tracker.Version;

        tracker.AddCharacter('h');

        Assert.True(tracker.Version > before);
    }

    [Fact]
    public void VersionRisesOnBackspace()
    {
        TypedWordTracker tracker = Type("hel");
        int before = tracker.Version;

        tracker.Backspace();

        Assert.True(tracker.Version > before);
    }

    [Fact]
    public void VersionRisesOnReset()
    {
        TypedWordTracker tracker = Type("hel");
        int before = tracker.Version;

        tracker.Reset();

        Assert.True(tracker.Version > before);
    }

    [Fact]
    public void VersionRisesOnResetEvenWithNothingTyped()
    {
        // A click with no word in progress still means the caret moved, so
        // anything already in flight has to be treated as out of date.
        var tracker = new TypedWordTracker();
        int before = tracker.Version;

        tracker.Reset();

        Assert.True(tracker.Version > before);
    }

    [Fact]
    public void VersionRisesOnClear()
    {
        TypedWordTracker tracker = Type("hel");
        int before = tracker.Version;

        tracker.Clear();

        Assert.True(tracker.Version > before);
    }

    [Fact]
    public void VersionRisesWhenASuggestionIsAccepted()
    {
        TypedWordTracker tracker = Type("hel");
        int before = tracker.Version;

        tracker.AcceptWord("hello");

        Assert.True(tracker.Version > before);
    }

    [Fact]
    public void VersionIsSettledByTheTimeWordFinishedIsRaised()
    {
        // This is the ordering the correction depends on. It reads the
        // version inside this event and checks it again later, so the number
        // must already account for the word ending. If it went up afterwards
        // every correction would look stale and none would ever apply.
        var tracker = new TypedWordTracker();
        int seen = -1;

        tracker.WordFinished += (_, _) => seen = tracker.Version;

        foreach (char c in "helo ")
        {
            tracker.AddCharacter(c);
        }

        Assert.Equal(tracker.Version, seen);
    }

    [Fact]
    public void VersionMovesOnWhenTypingContinuesAfterAWordEnds()
    {
        // The exact case that produced "hhello": the word ended, a correction
        // was being worked out, and the user kept typing. The captured number
        // and the current one must differ, so the correction is dropped.
        var tracker = new TypedWordTracker();
        int captured = -1;

        tracker.WordFinished += (_, _) => captured = tracker.Version;

        foreach (char c in "helo ")
        {
            tracker.AddCharacter(c);
        }

        tracker.AddCharacter('w');

        Assert.NotEqual(captured, tracker.Version);
    }

    // Whether the start of the current word was actually watched. This is the
    // rule that stops text being mangled, so it gets its own set.

    [Fact]
    public void AFreshTrackerDoesNotKnowWhereTheWordBegan()
    {
        // Nothing has been seen yet, so the caret could be anywhere, even in
        // the middle of a word already on screen.
        Assert.False(new TypedWordTracker().IsWordStartKnown);
    }

    [Fact]
    public void FinishingAWordMakesTheNextOneKnown()
    {
        TypedWordTracker tracker = Type("hello ");

        Assert.True(tracker.IsWordStartKnown);
    }

    [Fact]
    public void ResetLosesIt()
    {
        TypedWordTracker tracker = Type("hello ");
        tracker.Reset();

        Assert.False(tracker.IsWordStartKnown);
    }

    [Fact]
    public void TypingAfterAResetDoesNotWinItBack()
    {
        // The exact case that produced "hehello". The user was partway
        // through "hello", something moved the caret, and the letters counted
        // from then on are only the tail of what is on screen.
        TypedWordTracker tracker = Type("he");
        tracker.Reset();

        foreach (char c in "llo")
        {
            tracker.AddCharacter(c);
        }

        Assert.Equal("llo", tracker.CurrentWord);
        Assert.False(tracker.IsWordStartKnown);
    }

    [Fact]
    public void ASeparatorAfterAResetWinsItBack()
    {
        // One word is given up and then normal service resumes, which is the
        // whole cost of the rule.
        var tracker = new TypedWordTracker();
        tracker.Reset();

        foreach (char c in "llo ")
        {
            tracker.AddCharacter(c);
        }

        Assert.True(tracker.IsWordStartKnown);
    }

    [Fact]
    public void AcceptingAWordMakesItKnown()
    {
        // We put the word and its space there, so we know what is in front.
        TypedWordTracker tracker = Type("hel");
        tracker.Reset();
        tracker.AcceptWord("hello");

        Assert.True(tracker.IsWordStartKnown);
    }

    [Fact]
    public void BackspaceDoesNotWinItBack()
    {
        TypedWordTracker tracker = Type("he");
        tracker.Reset();
        tracker.AddCharacter('l');
        tracker.Backspace();

        Assert.False(tracker.IsWordStartKnown);
    }

    [Fact]
    public void TheFinishedWordCarriesWhetherItsStartWasKnown()
    {
        // The fixing happens a moment after the event, by which time the flag
        // describes the NEXT word, so the answer has to travel with the event
        // itself. Getting this backwards would correct the very words that
        // must be left alone.
        var tracker = new TypedWordTracker();
        var seen = new List<bool>();

        tracker.WordFinished += (_, e) => seen.Add(e.StartWasKnown);

        tracker.Reset();
        foreach (char c in "llo ")
        {
            tracker.AddCharacter(c);
        }

        foreach (char c in "world ")
        {
            tracker.AddCharacter(c);
        }

        // The first word was picked up mid flight, the second was watched
        // from its first letter.
        Assert.Equal(new[] { false, true }, seen);
    }
}
