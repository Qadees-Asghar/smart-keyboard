using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests.Engine;

public class FuzzyMatcherTests
{
    private static FuzzyMatcher BuildMatcher()
    {
        var trie = new Trie();
        var tree = new BKTree();

        // word, how common it is
        var words = new (string Word, int Count)[]
        {
            ("the", 1000), ("then", 300), ("them", 250), ("they", 200), ("she", 150),
            ("he", 900), ("be", 800), ("cat", 500), ("cut", 100), ("car", 400),
            ("cart", 90), ("care", 80), ("keyboard", 60), ("keyboards", 20),
            ("suggestion", 40), ("suggestions", 15), ("receive", 70),
            ("hello", 30), ("help", 900), ("held", 200), ("hero", 120),
            ("until", 400), ("untie", 20), ("running", 50), ("ruining", 10),
            ("can't", 208), ("canto", 6), ("can", 10739), ("want", 2236),
        };

        foreach ((string word, int count) in words)
        {
            trie.Insert(word, count);
            tree.Add(word);
        }

        return new FuzzyMatcher(tree, trie);
    }

    [Theory]
    [InlineData("cat", 1)]
    [InlineData("the", 1)]
    [InlineData("word", 1)]
    [InlineData("hello", 2)]
    [InlineData("keyboard", 2)]
    public void GetMaxDistance_UsesOneForShortWordsAndTwoForLongOnes(string word, int expected)
    {
        Assert.Equal(expected, FuzzyMatcher.GetMaxDistance(word));
    }

    [Fact]
    public void FindCorrections_SuggestsNothingForAWordThatIsSpelledRight()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.Empty(matcher.FindCorrections("the"));
        Assert.Empty(matcher.FindCorrections("keyboard"));
    }

    [Fact]
    public void FindCorrections_FindsTheRealWordBehindATypo()
    {
        FuzzyMatcher matcher = BuildMatcher();

        List<string> fixes = matcher.FindCorrections("keybord").Select(f => f.Word).ToList();

        Assert.Contains("keyboard", fixes);
    }

    [Fact]
    public void FindCorrections_PutsCloserWordsFirst()
    {
        FuzzyMatcher matcher = BuildMatcher();

        // "keybord" is long, so we look up to 2 edits away.
        // "keyboard" is 1 edit away, "keyboards" is 2 edits away.
        List<FuzzyMatch> fixes = matcher.FindCorrections("keybord");

        Assert.Equal("keyboard", fixes[0].Word);
        Assert.Equal(1, fixes[0].Distance);
        Assert.Contains(fixes, f => f.Word == "keyboards" && f.Distance == 2);

        List<int> distances = fixes.Select(f => f.Distance).ToList();
        Assert.Equal(distances.OrderBy(d => d).ToList(), distances);
    }

    [Fact]
    public void FindCorrections_BreaksATieByPickingTheMoreCommonWord()
    {
        FuzzyMatcher matcher = BuildMatcher();

        // "cst" is 1 edit from both "cat" (500) and "cut" (100).
        List<FuzzyMatch> fixes = matcher.FindCorrections("cst");

        Assert.Equal("cat", fixes[0].Word);
        Assert.Contains(fixes, f => f.Word == "cut");
    }

    [Fact]
    public void FindCorrections_FillsInHowCommonEachSuggestionIs()
    {
        FuzzyMatcher matcher = BuildMatcher();

        FuzzyMatch top = matcher.FindCorrections("cst").First();

        Assert.Equal(500, top.Frequency);
    }

    [Fact]
    public void FindCorrections_GivesAtMostFiveByDefault()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.True(matcher.FindCorrections("cae").Count <= 5);
    }

    [Fact]
    public void FindCorrections_RespectsARequestedCount()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.True(matcher.FindCorrections("cae", 2).Count <= 2);
    }

    [Fact]
    public void FindCorrections_StaysStrictOnShortWords()
    {
        FuzzyMatcher matcher = BuildMatcher();

        // "xe" is a 2 letter word, so only 1 edit away counts.
        // "he" and "be" qualify. "cat" is far away and must not show up.
        List<string> fixes = matcher.FindCorrections("xe").Select(f => f.Word).ToList();

        Assert.Contains("he", fixes);
        Assert.Contains("be", fixes);
        Assert.DoesNotContain("cat", fixes);
    }

    [Fact]
    public void FindCorrections_ReturnsNothingWhenNoWordIsCloseEnough()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.Empty(matcher.FindCorrections("zzzzzzzz"));
    }

    [Fact]
    public void FindCorrections_HandlesBlankInput()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.Empty(matcher.FindCorrections(null));
        Assert.Empty(matcher.FindCorrections(string.Empty));
        Assert.Empty(matcher.FindCorrections("   "));
    }

    [Fact]
    public void IsMisspelled_IsTrueOnlyForWordsNotInTheDictionary()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.True(matcher.IsMisspelled("keybord"));
        Assert.False(matcher.IsMisspelled("keyboard"));
        Assert.False(matcher.IsMisspelled("KeyBoard"));
        Assert.False(matcher.IsMisspelled(string.Empty));
    }

    [Fact]
    public void Add_MakesANewWordAvailableAsAFixStraightAway()
    {
        var trie = new Trie();
        var tree = new BKTree();
        trie.Insert("the", 100);
        tree.Add("the");

        var matcher = new FuzzyMatcher(tree, trie);
        Assert.Empty(matcher.FindCorrections("qadees"));

        trie.Insert("qadees", 1);
        matcher.Add("qadees");

        Assert.Contains("qadees", matcher.FindCorrections("qadee").Select(f => f.Word));
    }

    [Fact]
    public void GetLetterSwaps_BuildsEveryNeighbourSwap()
    {
        Assert.Equal(new[] { "eth", "the" }, FuzzyMatcher.GetLetterSwaps("teh").ToArray());
    }

    [Fact]
    public void GetLetterSwaps_SkipsSwappingTwoOfTheSameLetter()
    {
        // "lals" swaps at positions 0, 1 and 2.
        Assert.Equal(new[] { "alls", "llas", "lasl" }, FuzzyMatcher.GetLetterSwaps("lals").ToArray());

        // "aa" would only swap a with a, which changes nothing, so nothing comes back.
        Assert.Empty(FuzzyMatcher.GetLetterSwaps("aa"));
        Assert.Empty(FuzzyMatcher.GetLetterSwaps("a"));
        Assert.Empty(FuzzyMatcher.GetLetterSwaps(string.Empty));
    }

    [Fact]
    public void FindCorrections_FixesASwapEvenInAShortWord()
    {
        FuzzyMatcher matcher = BuildMatcher();

        // "teh" is 3 letters, so edit distance only looks 1 step away, and
        // "the" is 2 plain edits away. The swap rule is what finds it.
        List<FuzzyMatch> fixes = matcher.FindCorrections("teh");

        Assert.Equal("the", fixes[0].Word);
        Assert.Equal(1, fixes[0].Distance);
    }

    [Fact]
    public void FindCorrections_NeverListsTheSameWordTwice()
    {
        FuzzyMatcher matcher = BuildMatcher();

        List<string> words = matcher.FindCorrections("teh").Select(f => f.Word).ToList();

        Assert.Equal(words.Count, words.Distinct().Count());
    }

    [Fact]
    public void GetDoubleLetterFixes_FindsTheWordWithTheLetterTypedTwice()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.Contains("hello", matcher.GetDoubleLetterFixes("helo"));
        Assert.Contains("running", matcher.GetDoubleLetterFixes("runing"));
    }

    [Fact]
    public void GetDoubleLetterFixes_AlsoFindsTheWordWithOneLetterTakenAway()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.Contains("until", matcher.GetDoubleLetterFixes("untill"));
    }

    [Fact]
    public void GetDoubleLetterFixes_LeavesVeryShortWordsAlone()
    {
        FuzzyMatcher matcher = BuildMatcher();

        // "hel" is only 3 letters. Doubling a letter there is guessing far
        // too much from far too little, so nothing comes back.
        Assert.Empty(matcher.GetDoubleLetterFixes("hel"));
    }

    [Fact]
    public void FindCorrections_PutsTheDoubledLetterAheadOfACommonerWord()
    {
        FuzzyMatcher matcher = BuildMatcher();

        // "help" is 30 times commoner than "hello" and is also one edit away,
        // so frequency alone would answer "help". Missing one of a double
        // letter is the more likely slip, so "hello" has to come first.
        List<FuzzyMatch> fixes = matcher.FindCorrections("helo");

        Assert.Equal("hello", fixes[0].Word);
        Assert.True(fixes[0].IsDoubleLetterFix);
        Assert.Contains(fixes, f => f.Word == "help" && !f.IsDoubleLetterFix);
    }

    [Fact]
    public void FindCorrections_StillUsesFrequencyWhenNoDoubleLetterIsInvolved()
    {
        FuzzyMatcher matcher = BuildMatcher();

        // Nothing here is a doubled letter fix, so the commonest word wins
        // exactly as it did before the rule was added.
        List<FuzzyMatch> fixes = matcher.FindCorrections("thm");

        // "the", "them", "then" and "they" are all one edit away and none of
        // them is a doubled letter, so the commonest, "the", comes first.
        Assert.Equal("the", fixes[0].Word);
        Assert.False(fixes[0].IsDoubleLetterFix);
    }

    [Fact]
    public void GetApostropheFixes_FindsTheContraction()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.Contains("can't", matcher.GetApostropheFixes("cant"));
    }

    [Fact]
    public void GetApostropheFixes_IgnoresAWordThatAlreadyHasOne()
    {
        FuzzyMatcher matcher = BuildMatcher();

        Assert.Empty(matcher.GetApostropheFixes("can't"));
    }

    [Fact]
    public void FindCorrections_PutsTheContractionAheadOfAWordItStarts()
    {
        FuzzyMatcher matcher = BuildMatcher();

        // "cant" is the start of "canto", so the unfinished word rule would
        // pick it. But every letter of "can't" was typed correctly and only
        // the apostrophe is missing, which is the likelier slip by far.
        List<FuzzyMatch> fixes = matcher.FindCorrections("cant");

        Assert.Equal("can't", fixes[0].Word);
        Assert.True(fixes[0].IsMissingApostrophe);
        Assert.Equal(0, fixes[0].SlipRank);
    }
}
