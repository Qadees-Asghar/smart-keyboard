using System.Diagnostics;
using SmartKeyboard.Core;
using SmartKeyboard.Core.Engine;

namespace SmartKeyboard.Tests;

/// <summary>
/// Checks the speed targets from the project spec against the real dictionary.
/// The limits are generous on purpose, so a slow build machine does not make
/// the test fail for no reason.
///
/// Every timing here is the MEDIAN of several runs, never a single one. A
/// single reading was tried first and it failed about one run in four, not
/// because anything was slow but because the whole suite runs in parallel and
/// any one reading can catch a garbage collection or lose its turn on the
/// processor. The median says what the speed actually is, which is the thing
/// the target is about.
/// </summary>
[Collection("real dictionary")]
public class PerformanceTests
{
    private readonly RealDictionary _dictionary;

    public PerformanceTests(RealDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    [Fact]
    public void TheRealDictionaryActuallyLoads()
    {
        Assert.True(_dictionary.Words.WordCount > 10000, $"only {_dictionary.Words.WordCount} words loaded");
        Assert.True(_dictionary.Bigrams.PairCount > 10000, $"only {_dictionary.Bigrams.PairCount} pairs loaded");

        Assert.True(_dictionary.Words.Contains("program"));
        Assert.True(_dictionary.Words.Contains("computer"));
        Assert.True(_dictionary.Words.Contains("because"));
        Assert.False(_dictionary.Words.Contains("teh"));
    }

    [Fact]
    public void StartupIsWellUnderTenSeconds()
    {
        Assert.True(
            _dictionary.LoadTimeMs < 10000,
            $"loading took {_dictionary.LoadTimeMs} ms, the target is under 10000 ms");
    }

    [Fact]
    public void EachKeystrokeGivesSuggestionsInUnderOneHundredMilliseconds()
    {
        var engine = new SuggestionEngine(_dictionary.Words);

        // A single letter is the worst case, because it matches the most words.
        string[] prefixes = { "a", "s", "t", "pr", "pro", "prog", "progr" };

        // Warm up first, so the very first call does not measure JIT time.
        foreach (string prefix in prefixes)
        {
            engine.GetSuggestions(prefix);
        }

        foreach (string prefix in prefixes)
        {
            double taken = MedianMilliseconds(() => engine.GetSuggestions(prefix));
            List<WordEntry> results = engine.GetSuggestions(prefix);

            Assert.True(
                taken < 100,
                $"prefix '{prefix}' took {taken:0.0} ms, the target is under 100 ms");
            Assert.NotEmpty(results);
        }
    }

    [Fact]
    public void SuggestionsForARealPrefixLookSensible()
    {
        var engine = new SuggestionEngine(_dictionary.Words);

        List<string> words = engine.GetSuggestionWords("prog");

        Assert.Contains("program", words);
        Assert.All(words, w => Assert.StartsWith("prog", w));
    }

    [Fact]
    public void FixingATypoTakesUnderTwoHundredMilliseconds()
    {
        var matcher = new FuzzyMatcher(_dictionary.Tree, _dictionary.Words);

        string[] typos = { "teh", "recieve", "keybord", "sugestion", "definately", "seperate", "occured" };

        // Warm up so the first call does not measure JIT time.
        foreach (string typo in typos)
        {
            matcher.FindCorrections(typo);
        }

        foreach (string typo in typos)
        {
            double taken = MedianMilliseconds(() => matcher.FindCorrections(typo));

            Assert.True(
                taken < 200,
                $"fixing '{typo}' took {taken:0.0} ms, the target is under 200 ms");
        }
    }

    /// <summary>
    /// Runs something a few times and gives back the middle reading. See the
    /// note on this class for why a single reading is not good enough.
    /// </summary>
    private static double MedianMilliseconds(Action work)
    {
        const int Runs = 7;
        var times = new List<double>(Runs);

        for (int i = 0; i < Runs; i++)
        {
            var clock = Stopwatch.StartNew();
            work();
            clock.Stop();
            times.Add(clock.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        return times[Runs / 2];
    }

    [Fact]
    public void BuildingTheTypoTreeIsFastEnoughForStartup()
    {
        Assert.True(
            _dictionary.TreeBuildMs < 10000,
            $"building the BK tree took {_dictionary.TreeBuildMs} ms, the target is under 10000 ms");
    }

    [Fact]
    public void CommonTyposFindTheRightWord()
    {
        var matcher = new FuzzyMatcher(_dictionary.Tree, _dictionary.Words);

        Assert.Contains("the", matcher.FindCorrections("teh").Select(f => f.Word));
        Assert.Contains("receive", matcher.FindCorrections("recieve").Select(f => f.Word));
        Assert.Contains("keyboard", matcher.FindCorrections("keybord").Select(f => f.Word));
        Assert.Contains("separate", matcher.FindCorrections("seperate").Select(f => f.Word));
    }
}
