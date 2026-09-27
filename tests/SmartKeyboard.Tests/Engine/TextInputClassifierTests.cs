using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class TextInputClassifierTests
{
    [Fact]
    public void ACaretSettlesItWhateverElseIsTrue()
    {
        // Nothing reports a caret except somewhere text can be typed, so it
        // wins even over a class name on the "no" list.
        Assert.True(TextInputClassifier.TakesText(
            hasCaret: true, hasFocus: true, controlClass: "syslistview32"));
    }

    [Fact]
    public void NothingFocusedMeansNowhereToType()
    {
        Assert.False(TextInputClassifier.TakesText(
            hasCaret: false, hasFocus: false, controlClass: string.Empty));
    }

    [Theory]
    [InlineData("syslistview32")]
    [InlineData("systreeview32")]
    [InlineData("button")]
    [InlineData("listbox")]
    [InlineData("combobox")]
    [InlineData("directuihwnd")]
    [InlineData("progman")]
    [InlineData("workerw")]
    [InlineData("shell_traywnd")]
    public void KnownNonTextControlsAreRefused(string controlClass)
    {
        // The desktop, the taskbar and file lists. Pressing letters here is
        // what put the box on screen when nobody was writing.
        Assert.False(TextInputClassifier.TakesText(
            hasCaret: false, hasFocus: true, controlClass: controlClass));
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("richedit50w")]
    [InlineData("windowsforms10.edit.app.0.141b42a_r6_ad1")]
    [InlineData("scintilla")]
    public void KnownTextControlsAreAllowed(string controlClass)
    {
        Assert.True(TextInputClassifier.TakesText(
            hasCaret: false, hasFocus: true, controlClass: controlClass));
    }

    [Theory]
    [InlineData("chrome_renderwidgethosthwnd")]
    [InlineData("mozillawindowclass")]
    public void BrowsersAreAllowedBecauseWeCannotTell(string controlClass)
    {
        // Chromium reports no caret and one class for the whole page, so
        // refusing here would switch SmartKeyboard off in the apps people
        // write in most. This is a deliberate trade, not an oversight.
        Assert.True(TextInputClassifier.TakesText(
            hasCaret: false, hasFocus: true, controlClass: controlClass));
    }

    [Fact]
    public void AnUnknownControlIsAllowed()
    {
        // Permissive on purpose. An app where SmartKeyboard silently does
        // nothing is worse than an occasional box in the wrong place.
        Assert.True(TextInputClassifier.TakesText(
            hasCaret: false, hasFocus: true, controlClass: "somethingnobodyhasheardof"));
    }

    [Fact]
    public void CapitalsAndSpacesDoNotMatter()
    {
        Assert.False(TextInputClassifier.TakesText(
            hasCaret: false, hasFocus: true, controlClass: "  SysListView32  "));
    }

    [Fact]
    public void AFocusedControlThatWillNotSayItsNameIsAllowed()
    {
        Assert.True(TextInputClassifier.TakesText(
            hasCaret: false, hasFocus: true, controlClass: string.Empty));
    }

    [Fact]
    public void TheTextAndNonTextListsDoNotOverlap()
    {
        // An entry on both lists would make the answer depend on which is
        // checked first, which is exactly the kind of thing that rots.
        foreach (string text in TextInputClassifier.TextClasses)
        {
            Assert.DoesNotContain(text, TextInputClassifier.NonTextClasses);
        }
    }
}
