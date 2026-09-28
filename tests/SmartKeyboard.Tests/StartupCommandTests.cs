using SmartKeyboard.Core;

namespace SmartKeyboard.Tests;

public class StartupCommandTests
{
    private const string Exe = @"D:\tools\SmartKeyboard\SmartKeyboard.App.exe";

    [Fact]
    public void TheCommandQuotesThePathAndStartsQuietly()
    {
        Assert.Equal($"\"{Exe}\" --background", StartupCommand.Build(Exe));
    }

    [Fact]
    public void APathWithSpacesStaysInOnePiece()
    {
        const string spaced = @"D:\Antigrsvity workspace\SmartKeyboard\SmartKeyboard.App.exe";

        string command = StartupCommand.Build(spaced);

        Assert.Equal(spaced, StartupCommand.ProgramOf(command));
        Assert.True(StartupCommand.PointsTo(command, spaced));
    }

    [Fact]
    public void AnAlreadyQuotedPathIsNotQuotedTwice()
    {
        Assert.Equal($"\"{Exe}\" --background", StartupCommand.Build($"\"{Exe}\""));
    }

    [Fact]
    public void PathsAreComparedWithoutCaringAboutCase()
    {
        Assert.True(StartupCommand.PointsTo(StartupCommand.Build(Exe), Exe.ToUpperInvariant()));
    }

    [Fact]
    public void ACommandForAnotherCopyDoesNotPointHere()
    {
        string other = StartupCommand.Build(@"C:\elsewhere\SmartKeyboard.App.exe");

        Assert.False(StartupCommand.PointsTo(other, Exe));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(@"C:\a\b.exe --background", @"C:\a\b.exe")]
    [InlineData(@"""C:\a b\c.exe""", @"C:\a b\c.exe")]
    [InlineData(@"""C:\broken", @"C:\broken")]
    public void TheProgramIsReadBackOutOfACommand(string? command, string expected)
    {
        Assert.Equal(expected, StartupCommand.ProgramOf(command));
    }

    [Fact]
    public void AnEmptyPathIsRefused()
    {
        Assert.Throws<ArgumentException>(() => StartupCommand.Build("  "));
    }
}
