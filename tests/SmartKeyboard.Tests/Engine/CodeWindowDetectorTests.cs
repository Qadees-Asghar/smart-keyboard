using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class CodeWindowDetectorTests
{
    [Theory]
    [InlineData("Antigravity IDE")]
    [InlineData("Code")]
    [InlineData("cursor")]
    [InlineData("devenv")]
    [InlineData("idea64")]
    [InlineData("sublime_text")]
    [InlineData("notepad++")]
    [InlineData("vim")]
    public void KnowsTheEditors(string processName)
    {
        Assert.True(CodeWindowDetector.IsCodeWindow(processName));
    }

    [Theory]
    [InlineData("WindowsTerminal")]
    [InlineData("cmd")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    [InlineData("conhost")]
    [InlineData("mintty")]
    public void KnowsTheTerminals(string processName)
    {
        Assert.True(CodeWindowDetector.IsCodeWindow(processName));
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("msedge")]
    [InlineData("firefox")]
    [InlineData("notepad")]
    [InlineData("winword")]
    [InlineData("slack")]
    [InlineData("Discord")]
    [InlineData("explorer")]
    public void LeavesOrdinaryAppsAlone(string processName)
    {
        // These are exactly the apps SmartKeyboard is for, so a mistake here
        // would switch the whole program off where it is wanted.
        Assert.False(CodeWindowDetector.IsCodeWindow(processName));
    }

    [Fact]
    public void PlainNotepadIsNotACodeEditor()
    {
        // Worth its own test because "notepad" and "notepad++" differ by two
        // characters and mean opposite things here.
        Assert.False(CodeWindowDetector.IsCodeWindow("notepad"));
        Assert.True(CodeWindowDetector.IsCodeWindow("notepad++"));
    }

    [Theory]
    [InlineData("CODE")]
    [InlineData("code")]
    [InlineData("Code")]
    [InlineData("ANTIGRAVITY IDE")]
    public void CapitalsDoNotMatter(string processName)
    {
        Assert.True(CodeWindowDetector.IsCodeWindow(processName));
    }

    [Theory]
    [InlineData("code.exe")]
    [InlineData("cmd.exe")]
    [InlineData("Antigravity IDE.exe")]
    public void AnExeOnTheEndIsIgnored(string processName)
    {
        // Some ways of asking Windows give the name with ".exe" and some
        // without, so both have to work.
        Assert.True(CodeWindowDetector.IsCodeWindow(processName));
    }

    [Theory]
    [InlineData("codesign")]
    [InlineData("vimeo")]
    [InlineData("barcode")]
    [InlineData("powershelltoys")]
    public void MatchesTheWholeNameAndNotAPieceOfIt(string processName)
    {
        // The short names on the list would be dangerous as fragments.
        // "code" inside "barcode" must not switch SmartKeyboard off.
        Assert.False(CodeWindowDetector.IsCodeWindow(processName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotKnowingMeansCarryOnAsNormal(string? processName)
    {
        // When the process cannot be read we must not guess that it is an
        // editor, because that would silently stop the program working.
        Assert.False(CodeWindowDetector.IsCodeWindow(processName));
    }

    [Fact]
    public void SpacesAroundTheNameAreIgnored()
    {
        Assert.True(CodeWindowDetector.IsCodeWindow("  code  "));
    }
}
