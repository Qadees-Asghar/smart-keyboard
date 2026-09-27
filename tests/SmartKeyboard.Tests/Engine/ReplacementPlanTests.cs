using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class ReplacementPlanTests
{
    // The point of this class is to send fewer backspaces, because the
    // backspaces are the part that modern apps drop. Each case below says how
    // many are needed and what has to be typed after them.

    [Theory]
    [InlineData("helo ", "hello ", 2, "lo ")]
    [InlineData("cant ", "can't ", 2, "'t ")]
    [InlineData("wor", "world ", 0, "ld ")]
    [InlineData("hav ", "have ", 1, "e ")]
    public void KeepsTheLettersThatAreAlreadyRight(string typed, string wanted, int backspaces, string toType)
    {
        ReplacementPlan plan = ReplacementPlan.Compute(typed, wanted);

        Assert.Equal(backspaces, plan.Backspaces);
        Assert.Equal(toType, plan.ToType);
    }

    [Fact]
    public void FinishingAWordNeedsNoBackspacesAtAll()
    {
        // The best case, and the common one when a suggestion is accepted. A
        // key that is never sent cannot be dropped on the way.
        ReplacementPlan plan = ReplacementPlan.Compute("prog", "program ");

        Assert.Equal(0, plan.Backspaces);
        Assert.Equal("ram ", plan.ToType);
    }

    [Fact]
    public void ADifferentCapitalLetterReplacesTheWholeWord()
    {
        // This is why the comparison is case sensitive. If "t" and "T" were
        // treated as the same, the lower case "t" would be kept and the app
        // would end up with "the" when "The" was wanted.
        ReplacementPlan plan = ReplacementPlan.Compute("teh ", "The ");

        Assert.Equal(4, plan.Backspaces);
        Assert.Equal("The ", plan.ToType);
    }

    [Fact]
    public void AShorterReplacementJustDeletesTheExtra()
    {
        // "untill " and "until " agree for five letters, so the doubled l and
        // the space go, and only the space is typed back.
        ReplacementPlan plan = ReplacementPlan.Compute("untill ", "until ");

        Assert.Equal(2, plan.Backspaces);
        Assert.Equal(" ", plan.ToType);
    }

    [Fact]
    public void TextThatIsAlreadyRightDoesNothing()
    {
        ReplacementPlan plan = ReplacementPlan.Compute("hello ", "hello ");

        Assert.Equal(0, plan.Backspaces);
        Assert.Equal(string.Empty, plan.ToType);
        Assert.True(plan.IsNothingToDo);
    }

    [Fact]
    public void NothingTypedYetMeansJustTypeIt()
    {
        ReplacementPlan plan = ReplacementPlan.Compute(string.Empty, "hello ");

        Assert.Equal(0, plan.Backspaces);
        Assert.Equal("hello ", plan.ToType);
    }

    [Fact]
    public void AnEmptyReplacementRubsOutWhatIsThere()
    {
        ReplacementPlan plan = ReplacementPlan.Compute("oops", string.Empty);

        Assert.Equal(4, plan.Backspaces);
        Assert.Equal(string.Empty, plan.ToType);
    }

    [Fact]
    public void NullsAreTreatedAsEmpty()
    {
        ReplacementPlan plan = ReplacementPlan.Compute(null, null);

        Assert.True(plan.IsNothingToDo);
    }

    [Theory]
    [InlineData("helo ", "hello ")]
    [InlineData("teh ", "The ")]
    [InlineData("cant ", "can't ")]
    [InlineData("wor", "world ")]
    [InlineData("untill ", "until ")]
    [InlineData("", "hello ")]
    public void FollowingThePlanAlwaysGivesTheWantedText(string typed, string wanted)
    {
        // The property that actually matters: rubbing out that many letters
        // and typing the rest has to leave exactly the wanted text behind,
        // whatever the shortcut worked out.
        ReplacementPlan plan = ReplacementPlan.Compute(typed, wanted);

        string kept = typed[..(typed.Length - plan.Backspaces)];

        Assert.Equal(wanted, kept + plan.ToType);
    }

    [Fact]
    public void NeverAsksToDeleteMoreThanWasTyped()
    {
        foreach (string typed in new[] { "", "a", "hello", "helo " })
        {
            foreach (string wanted in new[] { "", "x", "hello ", "completely different" })
            {
                ReplacementPlan plan = ReplacementPlan.Compute(typed, wanted);

                Assert.InRange(plan.Backspaces, 0, typed.Length);
            }
        }
    }
}
