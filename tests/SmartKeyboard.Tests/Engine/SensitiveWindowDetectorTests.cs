using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

/// <summary>
/// System Wide Mode sees every key on the PC, so knowing when to stop
/// watching is the most important thing it does. These tests are the safety
/// net for that decision.
/// </summary>
public class SensitiveWindowDetectorTests
{
    [Theory]
    [InlineData("Enter your password")]
    [InlineData("Password Required")]
    [InlineData("PASSWORD")]
    [InlineData("Sign in to your account")]
    [InlineData("Login - MyBank")]
    [InlineData("Log in | GitHub")]
    [InlineData("Windows Security - Credentials")]
    [InlineData("Two factor authentication")]
    [InlineData("Bitwarden")]
    [InlineData("1Password 8")]
    [InlineData("KeePass Database")]
    [InlineData("BitLocker Drive Encryption")]
    [InlineData("Recovery phrase - MetaMask")]
    [InlineData("New Incognito Tab - Google Chrome")]
    public void WindowsThatLookLikeALoginStopTheWatching(string title)
    {
        Assert.True(
            SensitiveWindowDetector.LooksSensitive(title),
            $"\"{title}\" should have paused SmartKeyboard");
    }

    [Theory]
    [InlineData("Untitled - Notepad")]
    [InlineData("Document1 - Word")]
    [InlineData("SmartKeyboard")]
    [InlineData("WhatsApp")]
    [InlineData("Inbox - Outlook")]
    [InlineData("report.docx")]
    [InlineData("Visual Studio Code")]
    public void OrdinaryWindowsAreLeftAlone(string title)
    {
        Assert.False(
            SensitiveWindowDetector.LooksSensitive(title),
            $"\"{title}\" should not have paused SmartKeyboard");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankTitleIsNotTreatedAsSensitive(string? title)
    {
        // A window with no title is usually a background window. Pausing on
        // every one of them would make the feature useless.
        Assert.False(SensitiveWindowDetector.LooksSensitive(title));
    }

    [Fact]
    public void TheCheckDoesNotCareAboutCapitalLetters()
    {
        foreach (string word in SensitiveWindowDetector.SensitiveWords)
        {
            Assert.True(SensitiveWindowDetector.LooksSensitive(word.ToUpperInvariant()));
            Assert.True(SensitiveWindowDetector.LooksSensitive("My " + word + " window"));
        }
    }

    [Fact]
    public void TheWordCanBeAnywhereInTheTitle()
    {
        Assert.True(SensitiveWindowDetector.LooksSensitive("password at the start"));
        Assert.True(SensitiveWindowDetector.LooksSensitive("in the middle password here"));
        Assert.True(SensitiveWindowDetector.LooksSensitive("right at the end password"));
    }
}
