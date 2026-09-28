using SmartKeyboard.Core;

namespace SmartKeyboard.Tests;

public class LaunchOptionsTests
{
    [Fact]
    public void NoArgumentsMeansAPersonStartedIt()
    {
        // The desktop icon: open the window.
        Assert.Equal(LaunchMode.Normal, LaunchOptions.Parse(Array.Empty<string>()));
        Assert.Equal(LaunchMode.Normal, LaunchOptions.Parse(null));
    }

    [Theory]
    [InlineData("--background")]
    [InlineData("--BACKGROUND")]
    [InlineData(" --background ")]
    public void TheBackgroundFlagStartsQuietly(string flag)
    {
        Assert.Equal(LaunchMode.Background, LaunchOptions.Parse(new[] { flag }));
    }

    [Fact]
    public void TheEditorFlagStillOpensTheEditorAlone()
    {
        Assert.Equal(LaunchMode.EditorOnly, LaunchOptions.Parse(new[] { "--editor" }));
        Assert.Equal(LaunchMode.EditorOnly, LaunchOptions.Parse(new[] { "--background", "--editor" }));
    }

    [Fact]
    public void UnknownArgumentsAreIgnored()
    {
        Assert.Equal(LaunchMode.Normal, LaunchOptions.Parse(new[] { "--whatever", "" }));
        Assert.Equal(LaunchMode.Background, LaunchOptions.Parse(new[] { "x", "--background" }));
    }
}
