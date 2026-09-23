using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Tests.DataStructures;

public class EditDistanceTests
{
    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("flaw", "lawn", 2)]
    [InlineData("saturday", "sunday", 3)]
    [InlineData("teh", "the", 2)]
    [InlineData("recieve", "receive", 2)]
    [InlineData("cat", "cat", 0)]
    [InlineData("cat", "cut", 1)]
    [InlineData("cat", "cart", 1)]
    [InlineData("cart", "cat", 1)]
    public void Levenshtein_MatchesTheKnownAnswers(string a, string b, int expected)
    {
        Assert.Equal(expected, EditDistance.Levenshtein(a, b));
    }

    [Fact]
    public void Levenshtein_IsTheSameBothWays()
    {
        string[] words = { "keyboard", "keybord", "smart", "smrat", "hello", "" };

        foreach (string a in words)
        {
            foreach (string b in words)
            {
                Assert.Equal(EditDistance.Levenshtein(a, b), EditDistance.Levenshtein(b, a));
            }
        }
    }

    [Fact]
    public void Levenshtein_AgainstAnEmptyWordIsJustTheLength()
    {
        Assert.Equal(5, EditDistance.Levenshtein("hello", string.Empty));
        Assert.Equal(5, EditDistance.Levenshtein(string.Empty, "hello"));
        Assert.Equal(0, EditDistance.Levenshtein(string.Empty, string.Empty));
    }

    [Fact]
    public void Levenshtein_HandlesNullLikeAnEmptyWord()
    {
        Assert.Equal(3, EditDistance.Levenshtein(null, "cat"));
        Assert.Equal(3, EditDistance.Levenshtein("cat", null));
        Assert.Equal(0, EditDistance.Levenshtein(null, null));
    }

    [Fact]
    public void LevenshteinWithLimit_GivesTheRealAnswerWhenItIsInsideTheLimit()
    {
        Assert.Equal(1, EditDistance.LevenshteinWithLimit("cat", "cut", 2));
        Assert.Equal(0, EditDistance.LevenshteinWithLimit("cat", "cat", 2));
        Assert.Equal(2, EditDistance.LevenshteinWithLimit("teh", "the", 2));
    }

    [Fact]
    public void LevenshteinWithLimit_SaysOverTheLimitInsteadOfTheRealAnswer()
    {
        // The real distance is 3, and we only asked about 1.
        Assert.Equal(2, EditDistance.LevenshteinWithLimit("kitten", "sitting", 1));
    }

    [Fact]
    public void LevenshteinWithLimit_StopsStraightAwayOnABigLengthGap()
    {
        Assert.Equal(2, EditDistance.LevenshteinWithLimit("a", "abcdefgh", 1));
    }

    [Fact]
    public void LevenshteinWithLimit_AgreesWithTheFullVersionOnManyWordPairs()
    {
        string[] words =
        {
            "the", "teh", "cat", "cart", "keyboard", "keybord", "smart",
            "smrat", "suggestion", "sugestion", "a", "", "program", "porgram",
        };

        foreach (string a in words)
        {
            foreach (string b in words)
            {
                int real = EditDistance.Levenshtein(a, b);

                for (int limit = 0; limit <= 3; limit++)
                {
                    int limited = EditDistance.LevenshteinWithLimit(a, b, limit);
                    int expected = real <= limit ? real : limit + 1;

                    Assert.Equal(expected, limited);
                }
            }
        }
    }
}
