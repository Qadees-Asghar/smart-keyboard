using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class KeyHoldGateTests
{
    // The gate stands between the person's keystrokes and a word being
    // replaced. Each test below is a way it once lost a key, put one in the
    // wrong place, or could have left the keyboard dead.

    [Fact]
    public void KeysPassStraightThroughWhenNothingIsHolding()
    {
        var gate = new KeyHoldGate();

        Assert.False(gate.TryHold('a'));
        Assert.False(gate.IsHolding);
        Assert.Equal(0, gate.HeldCount);
    }

    [Fact]
    public void HeldKeysAreReplayedInTheOrderTheyWereTyped()
    {
        var gate = new KeyHoldGate();
        var replayed = new List<string>();

        gate.Begin();
        Assert.True(gate.TryHold('w'));
        Assert.True(gate.TryHold('o'));
        Assert.True(gate.TryHold(KeyHoldGate.Backspace));
        gate.End(replayed.Add);

        Assert.Equal(new[] { "wo\b" }, replayed);
        Assert.False(gate.IsHolding);
        Assert.Equal(0, gate.HeldCount);
    }

    [Fact]
    public void AHoldThatIsCalledOffStillGivesTheKeysBack()
    {
        // The bug: a correction abandoned part way returned without
        // replaying, and the held letter was typed at the NEXT replacement.
        var gate = new KeyHoldGate();
        var replayed = new List<string>();

        void CalledOffCorrection()
        {
            gate.Begin();
            try
            {
                gate.TryHold('w');
                return;
            }
            finally
            {
                gate.End(replayed.Add);
            }
        }

        CalledOffCorrection();

        Assert.Equal(new[] { "w" }, replayed);
        Assert.Equal(0, gate.HeldCount);

        // And a later replacement finds nothing stale waiting.
        var later = new List<string>();
        gate.Begin();
        gate.End(later.Add);
        Assert.Empty(later);
    }

    [Fact]
    public void AnInnerHoldEndingDoesNotReleaseTheOuterOne()
    {
        // The bug: a suggestion accepted while a typo fix was waiting
        // switched holding off for both.
        var gate = new KeyHoldGate();
        var replayed = new List<string>();

        gate.Begin();
        gate.TryHold('a');

        gate.Begin();
        gate.TryHold('b');
        gate.End(replayed.Add);

        Assert.Empty(replayed);
        Assert.True(gate.IsHolding);
        Assert.True(gate.TryHold('c'));

        gate.End(replayed.Add);

        Assert.Equal(new[] { "abc" }, replayed);
        Assert.False(gate.IsHolding);
    }

    [Fact]
    public void AKeyTypedDuringTheReplayWaitsBehindTheOthers()
    {
        // The bug: holding was switched off before the replay, so a key
        // pressed in between reached the app first and "wo" became "ow".
        var gate = new KeyHoldGate();
        var replayed = new List<string>();
        bool pressedDuringReplay = false;

        gate.Begin();
        gate.TryHold('w');

        gate.End(keys =>
        {
            replayed.Add(keys);

            if (!pressedDuringReplay)
            {
                pressedDuringReplay = true;

                // Still holding while the replay runs.
                Assert.True(gate.IsHolding);
                Assert.True(gate.TryHold('o'));
            }
        });

        Assert.Equal(new[] { "w", "o" }, replayed);
        Assert.False(gate.IsHolding);
        Assert.False(gate.TryHold('x'));
    }

    [Fact]
    public void KeysPastTheCapAreLetThroughRatherThanLost()
    {
        var gate = new KeyHoldGate();
        gate.Begin();

        for (int i = 0; i < KeyHoldGate.MaxHeld; i++)
        {
            Assert.True(gate.TryHold('a'));
        }

        Assert.False(gate.TryHold('b'));
        Assert.Equal(KeyHoldGate.MaxHeld, gate.HeldCount);

        gate.End(_ => { });
    }

    [Fact]
    public void AReplayThatFailsNeverLeavesTheKeyboardHolding()
    {
        var gate = new KeyHoldGate();

        gate.Begin();
        gate.TryHold('a');

        Assert.Throws<InvalidOperationException>(
            () => gate.End(_ => throw new InvalidOperationException()));

        Assert.False(gate.IsHolding);
        Assert.Equal(0, gate.HeldCount);
        Assert.False(gate.TryHold('b'));
    }

    [Fact]
    public void AHoldStartedByTheReplayTakesOverTheReplaying()
    {
        // A replayed space can finish a word that then needs fixing, which
        // starts a new hold from inside the replay. That hold replays the
        // rest; the outer one simply steps down.
        var gate = new KeyHoldGate();
        var replayed = new List<string>();

        gate.Begin();
        gate.TryHold(' ');

        gate.End(keys =>
        {
            replayed.Add(keys);
            gate.Begin();
            gate.TryHold('x');
        });

        Assert.Equal(new[] { " " }, replayed);
        Assert.True(gate.IsHolding);
        Assert.Equal(1, gate.HeldCount);

        gate.End(replayed.Add);

        Assert.Equal(new[] { " ", "x" }, replayed);
        Assert.False(gate.IsHolding);
    }

    [Fact]
    public void EndWithoutBeginDoesNothing()
    {
        var gate = new KeyHoldGate();
        var replayed = new List<string>();

        gate.End(replayed.Add);

        Assert.Empty(replayed);
        Assert.False(gate.IsHolding);
        Assert.False(gate.TryHold('a'));
    }

    [Fact]
    public async Task NoKeyIsLostOrDoubledWhenTypingRacesAReplacement()
    {
        // One thread types as fast as it can while another keeps starting
        // and ending replacements. Every key must come out exactly once:
        // either let through, or held and replayed.
        const int Keys = 20000;

        var gate = new KeyHoldGate();
        int passed = 0;
        int replayedCount = 0;
        bool typingDone = false;

        var replacer = Task.Run(() =>
        {
            while (!Volatile.Read(ref typingDone))
            {
                gate.Begin();
                try
                {
                    Thread.SpinWait(50);
                }
                finally
                {
                    gate.End(keys => Interlocked.Add(ref replayedCount, keys.Length));
                }
            }
        });

        for (int i = 0; i < Keys; i++)
        {
            if (!gate.TryHold('k'))
            {
                passed++;
            }
        }

        Volatile.Write(ref typingDone, true);
        await replacer.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(Keys, passed + Volatile.Read(ref replayedCount));
        Assert.Equal(0, gate.HeldCount);
        Assert.False(gate.IsHolding);
    }
}
