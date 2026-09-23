using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests;

/// <summary>
/// Autocorrect against the real 86,000 word dictionary.
///
/// A small test dictionary can make autocorrect look far better than it is.
/// What matters in real use is the opposite question: how often does it change
/// something it should have left alone.
/// </summary>
[Collection("real dictionary")]
public class AutocorrectOnRealDataTests
{
    private readonly RealDictionary _dictionary;

    public AutocorrectOnRealDataTests(RealDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    private AutocorrectEngine Engine()
    {
        var fuzzy = new FuzzyMatcher(_dictionary.Tree, _dictionary.Words);
        return new AutocorrectEngine(fuzzy, _dictionary.Words);
    }

    [Theory]
    [InlineData("teh", "the")]
    [InlineData("adn", "and")]
    [InlineData("taht", "that")]
    [InlineData("hte", "the")]
    [InlineData("recieve", "receive")]
    public void EverydayTyposAreFixed(string typo, string expected)
    {
        AutocorrectResult result = Engine().Check(typo);

        Assert.True(result.Changed, $"\"{typo}\" was left alone: {result.Reason}");
        Assert.Equal(expected, result.Corrected);
    }

    [Fact]
    public void AWholeCorrectSentenceIsLeftCompletelyAlone()
    {
        AutocorrectEngine engine = Engine();

        string[] words =
        {
            "hello", "my", "name", "is", "the", "keyboard", "is", "working",
            "just", "finally", "and", "it", "is", "a", "good", "experience",
            "and", "i", "am", "going", "to", "go", "very", "fast", "now",
        };

        foreach (string word in words)
        {
            AutocorrectResult result = engine.Check(word, isSentenceStart: true);
            Assert.False(result.Changed, $"\"{word}\" was wrongly changed to \"{result.Corrected}\"");
        }
    }

    [Fact]
    public void NamesInTheMiddleOfASentenceAreNeverTouched()
    {
        AutocorrectEngine engine = Engine();

        foreach (string name in new[] { "Qadees", "Asghar", "Seecs", "Nustian" })
        {
            AutocorrectResult result = engine.Check(name, isSentenceStart: false);

            Assert.False(result.Changed, $"the name \"{name}\" was changed to \"{result.Corrected}\"");
            Assert.Equal("looks like a name", result.Reason);
        }
    }

    [Fact]
    public void MostRealWordsAreNeverTouched()
    {
        AutocorrectEngine engine = Engine();

        int changed = 0;
        var examples = new List<string>();

        // Walk a slice of the real dictionary and count how many correct words
        // autocorrect would interfere with. The answer must be zero.
        foreach (var entry in _dictionary.Words.GetAllWords().Take(4000))
        {
            AutocorrectResult result = engine.Check(entry.Word, isSentenceStart: true);
            if (result.Changed)
            {
                changed++;
                if (examples.Count < 5)
                {
                    examples.Add($"{entry.Word} -> {result.Corrected}");
                }
            }
        }

        Assert.True(changed == 0, $"{changed} correct words were changed, for example: {string.Join(", ", examples)}");
    }

    [Fact]
    public void ShortNonsenseIsUsuallyLeftAloneRatherThanGuessedAt()
    {
        AutocorrectEngine engine = Engine();

        // Two letter nonsense sits one edit away from lots of real words, so
        // the 2x rule should refuse to pick a winner most of the time.
        int guessed = 0;
        foreach (string nonsense in new[] { "xe", "qe", "zi", "vu", "jo" })
        {
            if (engine.Check(nonsense).Changed)
            {
                guessed++;
            }
        }

        Assert.True(guessed <= 2, $"autocorrect guessed at {guessed} of 5 nonsense fragments");
    }

    // Most famous misspellings turn out to be a single mistake once a swap of
    // two letters counts as one. These are the ones that really need two.
    [Theory]
    [InlineData("seperatly", "separately")]
    public void MessierTyposInLongWordsAreFixedToo(string typo, string expected)
    {
        AutocorrectResult result = Engine().Check(typo);

        Assert.True(result.Changed, $"\"{typo}\" was left alone: {result.Reason}");
        Assert.Equal(expected, result.Corrected);
    }

    [Theory]
    [InlineData("experiance", "experience")]
    [InlineData("definately", "definitely")]
    [InlineData("occassion", "occasion")]
    [InlineData("goverment", "government")]
    [InlineData("enviroment", "environment")]
    [InlineData("neccessary", "necessary")]
    [InlineData("acheivement", "achievement")]
    public void TheCommonMisspellingsPeopleActuallyMakeAreFixed(string typo, string expected)
    {
        AutocorrectResult result = Engine().Check(typo);

        Assert.True(result.Changed, $"\"{typo}\" was left alone: {result.Reason}");
        Assert.Equal(expected, result.Corrected);
    }

    [Fact]
    public void ShortMessyWordsAreStillLeftAlone()
    {
        AutocorrectEngine engine = Engine();

        // Under 6 letters, two mistakes is too much of a guess.
        foreach (string word in new[] { "hallu", "wrold", "thnig" })
        {
            AutocorrectResult result = engine.Check(word);

            if (result.Changed)
            {
                // Only allowed if it was really just one mistake, such as a swap.
                Assert.True(
                    result.Reason == "corrected",
                    $"\"{word}\" became \"{result.Corrected}\"");
            }
        }
    }

    [Fact]
    public void TurningOffMessyFixingLeavesOnlyTheSafestCorrections()
    {
        AutocorrectEngine engine = Engine();
        engine.FixMessyWords = false;

        // One mistake words still get fixed.
        Assert.True(engine.Check("teh").Changed);

        // "seperatly" really is two mistakes away, so it is left alone.
        AutocorrectResult messy = engine.Check("seperatly");
        Assert.False(messy.Changed);
        Assert.Equal("too messy to fix on its own", messy.Reason);
    }

    [Theory]
    [InlineData("helo", "hello")]
    [InlineData("runing", "running")]
    [InlineData("stoped", "stopped")]
    [InlineData("untill", "until")]
    [InlineData("allmost", "almost")]
    public void AMissedDoubleLetterIsFixed(string typo, string expected)
    {
        // These are all one edit away from several real words, and in some
        // cases the wanted word is not the commonest of them. "helo" sits one
        // edit from "help", "held", "hero" and "hello", and "help" is by far
        // the commonest, so frequency alone answers "help". The doubled
        // letter rule is what makes these come out right.
        AutocorrectResult result = Engine().Check(typo);

        Assert.True(result.Changed, $"\"{typo}\" was left alone: {result.Reason}");
        Assert.Equal(expected, result.Corrected);
    }

    [Theory]
    [InlineData("hav", "have")]
    [InlineData("hous", "house")]
    [InlineData("wor", "work")]
    [InlineData("goin", "going")]
    [InlineData("wher", "where")]
    [InlineData("becaus", "because")]
    [InlineData("peopl", "people")]
    [InlineData("somethin", "something")]
    public void AWordThatWasNotFinishedIsCompleted(string typo, string expected)
    {
        // Every one of these used to fail, and two of them failed badly.
        // "hav" is one edit from "have", "has" and "had", so no count could
        // separate them and it gave up. "wor" became "for", because "for" is
        // fourteen times commoner than "work" and also one edit away.
        //
        // What decides it is that "have" and "work" keep every letter that
        // was actually pressed, while "has" and "for" claim a letter the user
        // typed was a mistake. Stopping early is the likelier slip.
        AutocorrectResult result = Engine().Check(typo);

        Assert.True(result.Changed, $"\"{typo}\" was left alone: {result.Reason}");
        Assert.Equal(expected, result.Corrected);
    }

    [Fact]
    public void ASwappedPairStillBeatsAnUnfinishedWord()
    {
        // "aer" is the start of "aero", and it is also "are" with two letters
        // the wrong way round. Both keys were pressed in the swap, so it is
        // the likelier slip and has to win.
        AutocorrectResult result = Engine().Check("aer");

        Assert.True(result.Changed);
        Assert.Equal("are", result.Corrected);
    }

    [Theory]
    [InlineData("bro")]
    [InlineData("wassup")]
    [InlineData("lol")]
    [InlineData("omg")]
    [InlineData("kinda")]
    [InlineData("screenshot")]
    [InlineData("emoji")]
    [InlineData("can't")]
    [InlineData("i've")]
    [InlineData("y'all")]
    public void WordsPeopleTypeToEachOtherAreLeftAlone(string word)
    {
        AutocorrectResult result = Engine().Check(word);

        Assert.False(result.Changed, $"\"{word}\" was changed into \"{result.Corrected}\"");
        Assert.Equal("already a real word", result.Reason);
    }

    [Theory]
    [InlineData("cant", "can't")]
    [InlineData("dont", "don't")]
    [InlineData("isnt", "isn't")]
    [InlineData("didnt", "didn't")]
    [InlineData("wasnt", "wasn't")]
    [InlineData("couldnt", "couldn't")]
    [InlineData("wouldnt", "wouldn't")]
    [InlineData("shouldnt", "shouldn't")]
    [InlineData("doesnt", "doesn't")]
    [InlineData("havent", "haven't")]
    [InlineData("arent", "aren't")]
    [InlineData("youre", "you're")]
    [InlineData("theyre", "they're")]
    [InlineData("thats", "that's")]
    [InlineData("whats", "what's")]
    [InlineData("hes", "he's")]
    [InlineData("shes", "she's")]
    [InlineData("ive", "i've")]
    [InlineData("im", "i'm")]
    public void AnApostropheLeftOutIsPutBack(string typo, string expected)
    {
        // Every letter here was typed correctly and in the right order, and
        // only the apostrophe is missing, which is the key people skip most.
        //
        // The counts are no help at all and often point the wrong way:
        // "your" is used 17826 times against 159 for "you're", and "that" is
        // used 29392 times against 370 for "that's". Left to popularity these
        // came out as "your", "there" and "that". "cant" was worse: it became
        // "canto", a word used six times in the whole list, because "cant"
        // happens to be the start of it.
        AutocorrectResult result = Engine().Check(typo);

        Assert.True(result.Changed, $"\"{typo}\" was left alone: {result.Reason}");
        Assert.Equal(expected, result.Corrected);
    }

    [Theory]
    [InlineData("its")]
    [InlineData("lets")]
    [InlineData("were")]
    [InlineData("well")]
    [InlineData("shell")]
    [InlineData("hell")]
    [InlineData("ill")]
    [InlineData("wed")]
    public void AWordThatIsAlsoAContractionWithoutItsMarkIsLeftAlone(string word)
    {
        // "its" and "it's" are both real, and so are "were" and "we're".
        // There is no way to tell which was meant, and quietly rewriting one
        // into the other would be worse than leaving it. The rule that saves
        // this is the oldest one: a word already in the dictionary is never
        // touched.
        AutocorrectResult result = Engine().Check(word);

        Assert.False(result.Changed, $"\"{word}\" became \"{result.Corrected}\"");
    }
}
