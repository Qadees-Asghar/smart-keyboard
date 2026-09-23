using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

/// <summary>
/// The editor hides the suggestion list when there is nothing to show.
/// These tests check that "nothing to show" only happens when the user really
/// has finished a word, not in the middle of typing one.
/// </summary>
[Collection("real dictionary")]
public class SuggestionVisibilityTests
{
    private readonly RealDictionary _dictionary;

    public SuggestionVisibilityTests(RealDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    private SuggestionEngine Engine() => new(_dictionary.Words);

    [Theory]
    [InlineData("experience")]
    [InlineData("keyboard")]
    [InlineData("suggestion")]
    [InlineData("because")]
    [InlineData("finally")]
    public void SuggestionsStayVisibleForEveryLetterOfARealWord(string word)
    {
        SuggestionEngine engine = Engine();

        for (int i = 1; i <= word.Length; i++)
        {
            string prefix = word.Substring(0, i);

            Assert.True(
                engine.GetSuggestions(prefix).Count > 0,
                $"the list went empty at \"{prefix}\" while typing \"{word}\"");
        }
    }

    [Fact]
    public void AFullyTypedWordStillShowsItselfInTheList()
    {
        SuggestionEngine engine = Engine();

        // This is the case that used to empty the list. "experience" is the
        // only word starting with "experience", and it was being filtered out
        // for being the same as what the user typed.
        List<string> words = engine.GetSuggestionWords("experience");

        Assert.Contains("experience", words);
    }

    [Fact]
    public void ThereIsNothingToShowForAPrefixNoWordStartsWith()
    {
        SuggestionEngine engine = Engine();

        Assert.Empty(engine.GetSuggestions("qqzzx"));
    }

    [Fact]
    public void ThereIsNothingToShowOnceTheWordIsFinished()
    {
        // After a space the prefix is empty, which is when the editor hides
        // the list. This is the only hide the user should ever notice.
        Assert.Equal(string.Empty, WordScanner.GetCurrentPrefix("experience ", 11));
        Assert.Empty(Engine().GetSuggestions(string.Empty));
    }
}
