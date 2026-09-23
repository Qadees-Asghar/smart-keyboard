namespace SmartKeyboard.Tests;

/// <summary>
/// Guards the quality of words.txt itself.
///
/// A spell checker is only as good as its word list. Junk words like "ot" or
/// "nt" mean real typos are never flagged, which is exactly the bug these
/// tests were written for.
/// </summary>
[Collection("real dictionary")]
public class DictionaryQualityTests
{
    private readonly RealDictionary _dictionary;

    public DictionaryQualityTests(RealDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    [Theory]
    [InlineData("ot")]
    [InlineData("ts")]
    [InlineData("nt")]
    [InlineData("ll")]
    [InlineData("ve")]
    [InlineData("o")]
    [InlineData("b")]
    [InlineData("z")]
    public void JunkFragmentsAreNotTreatedAsWords(string fragment)
    {
        Assert.False(
            _dictionary.Words.Contains(fragment),
            $"\"{fragment}\" is not a word and must not be in the dictionary");
    }

    [Theory]
    [InlineData("im")]
    [InlineData("dont")]
    [InlineData("cant")]
    [InlineData("whats")]
    [InlineData("youre")]
    [InlineData("doesnt")]
    public void ContractionsMissingTheirApostropheAreTreatedAsMisspelled(string broken)
    {
        Assert.False(
            _dictionary.Words.Contains(broken),
            $"\"{broken}\" is missing an apostrophe and must be flagged");
    }

    [Theory]
    [InlineData("iii")]
    [InlineData("vii")]
    [InlineData("xii")]
    [InlineData("xxx")]
    public void ChapterNumbersFromTheBooksAreNotWords(string numeral)
    {
        Assert.False(_dictionary.Words.Contains(numeral));
    }

    [Fact]
    public void OnlyAAndICountAsSingleLetterWords()
    {
        Assert.True(_dictionary.Words.Contains("a"));
        Assert.True(_dictionary.Words.Contains("i"));

        foreach (char letter in "bcdefghjklmnopqrstuvwxyz")
        {
            Assert.False(
                _dictionary.Words.Contains(letter.ToString()),
                $"\"{letter}\" must not count as a word");
        }
    }

    [Theory]
    [InlineData("the")]
    [InlineData("and")]
    [InlineData("did")]
    [InlineData("mix")]
    [InlineData("mid")]
    [InlineData("six")]
    [InlineData("to")]
    [InlineData("of")]
    [InlineData("go")]
    [InlineData("us")]
    [InlineData("program")]
    [InlineData("computer")]
    [InlineData("keyboard")]
    [InlineData("because")]
    [InlineData("experience")]
    public void RealWordsAreStillThere(string word)
    {
        Assert.True(_dictionary.Words.Contains(word), $"\"{word}\" went missing from the dictionary");
    }

    [Theory]
    [InlineData("don't")]
    [InlineData("it's")]
    [InlineData("i'll")]
    [InlineData("i've")]
    public void ProperContractionsWithApostrophesAreKept(string word)
    {
        Assert.True(_dictionary.Words.Contains(word));
    }

    [Fact]
    public void TheDictionaryIsStillBigEnoughToBeUseful()
    {
        Assert.True(
            _dictionary.Words.WordCount > 20000,
            $"only {_dictionary.Words.WordCount} words survived the filters");
    }

    [Theory]
    [InlineData("bro")]
    [InlineData("wassup")]
    [InlineData("lol")]
    [InlineData("omg")]
    [InlineData("btw")]
    [InlineData("pls")]
    [InlineData("yep")]
    [InlineData("nah")]
    [InlineData("kinda")]
    [InlineData("gonna")]
    [InlineData("wanna")]
    [InlineData("dunno")]
    [InlineData("selfie")]
    [InlineData("emoji")]
    [InlineData("meme")]
    [InlineData("screenshot")]
    [InlineData("hashtag")]
    [InlineData("podcast")]
    public void WordsPeopleTypeToEachOtherAreInTheDictionary(string word)
    {
        Assert.True(_dictionary.Words.Contains(word), $"\"{word}\" is missing");
    }

    [Theory]
    [InlineData("can't")]
    [InlineData("i've")]
    [InlineData("don't")]
    [InlineData("won't")]
    [InlineData("it's")]
    [InlineData("i'm")]
    [InlineData("you're")]
    [InlineData("didn't")]
    [InlineData("that's")]
    [InlineData("let's")]
    [InlineData("y'all")]
    [InlineData("o'clock")]
    public void ContractionsAreInTheDictionary(string word)
    {
        Assert.True(_dictionary.Words.Contains(word), $"\"{word}\" is missing");
    }

    [Theory]
    [InlineData("ill")]
    [InlineData("hen")]
    [InlineData("owl")]
    [InlineData("hug")]
    [InlineData("jaw")]
    [InlineData("pea")]
    [InlineData("shy")]
    [InlineData("vet")]
    [InlineData("wok")]
    [InlineData("oar")]
    [InlineData("sob")]
    [InlineData("jog")]
    [InlineData("nap")]
    [InlineData("mug")]
    [InlineData("rib")]
    [InlineData("owe")]
    public void EverydayShortWordsAreInTheDictionary(string word)
    {
        // These were all missing. Most were cut by a rule that threw away any
        // three letter word below a count of 50, which turned out to be far
        // too strict: it lost 51 of 252 everyday words. "ill" was missing for
        // a different reason, it was listed as the apostrophe free spelling
        // of "I'll", which forgot that it is an ordinary word as well.
        Assert.True(_dictionary.Words.Contains(word), $"\"{word}\" is missing");
    }

    [Theory]
    [InlineData("vell")]
    [InlineData("ened")]
    [InlineData("grat")]
    [InlineData("agin")]
    [InlineData("absorbtion")]
    [InlineData("accomodate")]
    [InlineData("liason")]
    [InlineData("auxillary")]
    [InlineData("suprising")]
    [InlineData("prefered")]
    public void KnownMisspellingsAreNotInTheDictionary(string word)
    {
        // A curated human list of misspellings is used on top of the rule
        // about rare words next to common ones. The rule cannot be made any
        // stricter: measured on four letter words it would have deleted
        // "deft", "dour", "ewer", "fret", "hone", "laud", "pare" and "wane"
        // to catch nine misspellings.
        Assert.False(_dictionary.Words.Contains(word), $"\"{word}\" should not be a word");
    }

    [Fact]
    public void AnInformalSpellingPeopleActuallyUseSurvivesTheMisspellingList()
    {
        // "thru" is on the misspelling list as a misspelling of "through",
        // but people write it on purpose and it is common enough here to
        // prove it, so it stays.
        Assert.True(_dictionary.Words.Contains("thru"));
    }
}
