using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class TypingSessionTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ANewSessionIsNotLive()
    {
        // Nothing has been typed, so a key pressed now is not part of writing.
        Assert.False(new TypingSession().IsLive(Start));
    }

    [Fact]
    public void LiveAsSoonAsSomethingIsTyped()
    {
        var session = new TypingSession();
        session.NoteKey(Start);

        Assert.True(session.IsLive(Start));
    }

    [Fact]
    public void StillLiveJustInsideTheTimeout()
    {
        var session = new TypingSession();
        session.NoteKey(Start);

        DateTime almost = Start + TypingSession.IdleTimeout - TimeSpan.FromMilliseconds(1);

        Assert.True(session.IsLive(almost));
    }

    [Fact]
    public void DeadOnceTheTimeoutHasPassed()
    {
        var session = new TypingSession();
        session.NoteKey(Start);

        Assert.False(session.IsLive(Start + TypingSession.IdleTimeout));
    }

    [Fact]
    public void CarryingOnTypingKeepsItAlive()
    {
        // Two seconds between keys is slow typing, not stopping, so the
        // session must survive it.
        var session = new TypingSession();
        session.NoteKey(Start);

        DateTime later = Start + TimeSpan.FromSeconds(2);
        session.NoteKey(later);

        Assert.True(session.IsLive(later + TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void EndStopsItStraightAway()
    {
        // A click or a change of window means the user has gone elsewhere,
        // and waiting out the timeout would leave the box up meanwhile.
        var session = new TypingSession();
        session.NoteKey(Start);
        session.End();

        Assert.False(session.IsLive(Start));
    }

    [Fact]
    public void TypingAgainAfterEndStartsAFreshSession()
    {
        var session = new TypingSession();
        session.NoteKey(Start);
        session.End();
        session.NoteKey(Start + TimeSpan.FromSeconds(10));

        Assert.True(session.IsLive(Start + TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void TheTimeoutIsLongEnoughToThinkButShortEnoughToNotice()
    {
        // Guards the value itself. Too short and the box vanishes while the
        // user pauses mid sentence; too long and it lingers after they stop.
        Assert.InRange(TypingSession.IdleTimeout.TotalSeconds, 1.5, 5.0);
    }
}
