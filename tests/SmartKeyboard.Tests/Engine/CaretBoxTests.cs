using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class CaretBoxTests
{
    // Where the popup goes in apps that draw their own caret. Every box that
    // is refused below is one an app really can hand back, and believing it
    // would put the popup somewhere the user is not typing.

    private static readonly ScreenBox Field = new(100, 200, 600, 300);

    // What an app that will not say how big its text field is hands back.
    private static readonly ScreenBox NoField = default;

    [Fact]
    public void TheCaretIsTheRightEdgeOfTheCharacterBeforeIt()
    {
        ScreenBox? caret = CaretBox.FromCharBefore(new[] { new ScreenBox(300, 250, 9, 20) }, "o");

        Assert.Equal(new ScreenBox(309, 250, 0, 20), caret);
    }

    [Fact]
    public void TheCaretIsTheLeftEdgeOfTheCharacterAfterIt()
    {
        ScreenBox? caret = CaretBox.FromCharAfter(new[] { new ScreenBox(300, 250, 9, 20) });

        Assert.Equal(new ScreenBox(300, 250, 0, 20), caret);
    }

    [Fact]
    public void AnEmptyRangeAtTheCaretIsUsedAsItIs()
    {
        ScreenBox? caret = CaretBox.FromCaret(new[] { new ScreenBox(412, 250, 1, 20) });

        Assert.Equal(new ScreenBox(412, 250, 0, 20), caret);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public void ALineBreakBeforeTheCaretIsNotTrusted(string lineBreak)
    {
        // Its box is at the end of the line above, not where the caret is.
        Assert.Null(CaretBox.FromCharBefore(new[] { new ScreenBox(650, 230, 5, 20) }, lineBreak));
    }

    [Fact]
    public void ACharacterSplitOverTwoLinesUsesTheBoxNearestTheCaret()
    {
        ScreenBox[] boxes = { new(600, 230, 8, 20), new(110, 250, 8, 20) };

        Assert.Equal(118, CaretBox.FromCharBefore(boxes, "a")!.Value.Left);
        Assert.Equal(600, CaretBox.FromCharAfter(boxes)!.Value.Left);
    }

    [Fact]
    public void NoBoxesMeansNoCaret()
    {
        Assert.Null(CaretBox.FromCaret(Array.Empty<ScreenBox>()));
        Assert.Null(CaretBox.FromCaret(null));
        Assert.Null(CaretBox.FromCharBefore(Array.Empty<ScreenBox>(), "a"));
        Assert.Null(CaretBox.FromCharAfter(null));
    }

    [Fact]
    public void ACaretInsideTheFieldIsUsable()
    {
        Assert.True(CaretBox.IsUsable(new ScreenBox(309, 250, 0, 20), Field));
    }

    [Fact]
    public void NothingIsNotUsable()
    {
        Assert.False(CaretBox.IsUsable(null, Field));
    }

    [Fact]
    public void TheAllZeroAnswerIsNotUsable()
    {
        // What an app that does not really know hands back.
        Assert.False(CaretBox.IsUsable(new ScreenBox(0, 0, 0, 20), Field));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(CaretBox.MaxLineHeight + 1)]
    public void ABoxWithoutARealLineHeightIsNotUsable(double height)
    {
        Assert.False(CaretBox.IsUsable(new ScreenBox(309, 250, 0, height), NoField));
    }

    [Fact]
    public void ABoxFarOutsideTheFieldIsNotUsable()
    {
        Assert.False(CaretBox.IsUsable(new ScreenBox(1500, 250, 0, 20), Field));
        Assert.False(CaretBox.IsUsable(new ScreenBox(309, 900, 0, 20), Field));
    }

    [Fact]
    public void ABoxJustOutsideTheFieldEdgeIsStillUsable()
    {
        // A caret at the very end of a line can sit on the border.
        Assert.True(CaretBox.IsUsable(new ScreenBox(Field.Right + 2, 250, 0, 20), Field));
    }

    [Fact]
    public void WhenTheFieldIsUnknownOnlyTheOtherChecksApply()
    {
        Assert.True(CaretBox.IsUsable(new ScreenBox(1500, 900, 0, 20), NoField));
    }

    [Fact]
    public void NonsenseNumbersAreNotUsable()
    {
        Assert.False(CaretBox.IsUsable(new ScreenBox(double.NaN, 250, 0, 20), Field));
        Assert.False(CaretBox.IsUsable(new ScreenBox(309, double.PositiveInfinity, 0, 20), Field));
    }
}
