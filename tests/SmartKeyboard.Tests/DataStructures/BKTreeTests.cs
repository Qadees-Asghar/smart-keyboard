using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Tests.DataStructures;

public class BKTreeTests
{
    private static readonly string[] SampleWords =
    {
        "book", "books", "boo", "boon", "cook", "cake", "cape", "cart",
        "back", "backs", "boot", "hello", "help", "held", "hold", "the",
        "teh", "then", "them", "they",
    };

    private static BKTree BuildSample()
    {
        var tree = new BKTree();
        tree.AddRange(SampleWords);
        return tree;
    }

    [Fact]
    public void Add_CountsEachWordOnce()
    {
        BKTree tree = BuildSample();

        Assert.Equal(SampleWords.Length, tree.Count);
    }

    [Fact]
    public void Add_IgnoresAWordThatIsAlreadyThere()
    {
        var tree = new BKTree();
        tree.Add("book");
        tree.Add("book");
        tree.Add("BOOK");

        Assert.Equal(1, tree.Count);
    }

    [Fact]
    public void Add_IgnoresBlankWords()
    {
        var tree = new BKTree();
        tree.Add(null);
        tree.Add(string.Empty);
        tree.Add("   ");

        Assert.True(tree.IsEmpty);
        Assert.Equal(0, tree.Count);
    }

    [Fact]
    public void Search_FindsWordsOneEditAway()
    {
        BKTree tree = BuildSample();

        List<string> found = tree.Search("boo", 1).Select(m => m.Word).OrderBy(w => w).ToList();

        Assert.Equal(new List<string> { "boo", "book", "boon", "boot" }, found);
    }

    [Fact]
    public void Search_ReportsTheRightDistanceForEachMatch()
    {
        BKTree tree = BuildSample();

        FuzzyMatch exact = tree.Search("book", 2).Single(m => m.Word == "book");
        FuzzyMatch oneAway = tree.Search("book", 2).Single(m => m.Word == "cook");

        Assert.Equal(0, exact.Distance);
        Assert.Equal(1, oneAway.Distance);
    }

    [Fact]
    public void Search_WithDistanceZeroFindsOnlyTheExactWord()
    {
        BKTree tree = BuildSample();

        Assert.Equal(new[] { "book" }, tree.Search("book", 0).Select(m => m.Word));
    }

    [Fact]
    public void Search_ReturnsNothingForABlankQueryOrAnEmptyTree()
    {
        Assert.Empty(BuildSample().Search(string.Empty, 2));
        Assert.Empty(BuildSample().Search(null, 2));
        Assert.Empty(new BKTree().Search("book", 2));
    }

    [Fact]
    public void Search_IgnoresCase()
    {
        BKTree tree = BuildSample();

        Assert.Equal(tree.Search("book", 1).Count, tree.Search("BOOK", 1).Count);
    }

    [Fact]
    public void Contains_IsTrueOnlyForStoredWords()
    {
        BKTree tree = BuildSample();

        Assert.True(tree.Contains("book"));
        Assert.False(tree.Contains("zebra"));
    }

    // This is the important one. The BK tree skips whole branches to go fast.
    // If the skipping rule were wrong, it would quietly miss real matches.
    // So we compare it against the slow method that checks every single word.
    [Fact]
    public void Search_FindsExactlyTheSameWordsAsCheckingEveryWordOneByOne()
    {
        BKTree tree = BuildSample();
        string[] queries = { "boo", "book", "cak", "hel", "the", "teh", "xyz", "b", "backs", "cooks" };

        foreach (string query in queries)
        {
            for (int maxDistance = 0; maxDistance <= 3; maxDistance++)
            {
                List<string> fromTree = tree.Search(query, maxDistance)
                    .Select(m => m.Word)
                    .OrderBy(w => w, StringComparer.Ordinal)
                    .ToList();

                List<string> bruteForce = SampleWords
                    .Where(w => EditDistance.Levenshtein(query, w) <= maxDistance)
                    .OrderBy(w => w, StringComparer.Ordinal)
                    .ToList();

                Assert.Equal(bruteForce, fromTree);
            }
        }
    }

    // Same check again, but on a bigger random word list, so we are not just
    // lucky with the twenty words above.
    [Fact]
    public void Search_MatchesBruteForceOnAThousandRandomWords()
    {
        var random = new Random(12345);
        var words = new HashSet<string>();
        while (words.Count < 1000)
        {
            int length = random.Next(2, 9);
            var letters = new char[length];
            for (int i = 0; i < length; i++)
            {
                letters[i] = (char)('a' + random.Next(0, 8));
            }

            words.Add(new string(letters));
        }

        var tree = new BKTree();
        tree.AddRange(words);

        Assert.Equal(words.Count, tree.Count);

        foreach (string query in words.Take(25))
        {
            for (int maxDistance = 0; maxDistance <= 2; maxDistance++)
            {
                List<string> fromTree = tree.Search(query, maxDistance)
                    .Select(m => m.Word)
                    .OrderBy(w => w, StringComparer.Ordinal)
                    .ToList();

                List<string> bruteForce = words
                    .Where(w => EditDistance.Levenshtein(query, w) <= maxDistance)
                    .OrderBy(w => w, StringComparer.Ordinal)
                    .ToList();

                Assert.Equal(bruteForce, fromTree);
            }
        }
    }
}
