using SmartKeyboard.Core.DataStructures;
using SmartKeyboard.Core.Engine;
using Xunit.Abstractions;

namespace SmartKeyboard.Tests.Engine;

/// <summary>
/// Measures how good autocorrect is on the real dictionary, rather than
/// checking one word at a time.
///
/// Every case in Benchmarks/autocorrect_cases.txt is run through the engine
/// and lands in one of four piles:
///
///   fixed right   a typo, and the word that was meant came out
///   fixed wrong   something was changed into the wrong word
///   missed        a typo that was left alone
///   kept          a correct word that was rightly left alone
///
/// Fixed wrong is the number that matters most. A missed typo is a red
/// underline; a wrong fix silently changes what someone wrote.
///
/// The limits below are set from the last measured run, so a change that
/// makes things worse fails here even if every single word test still passes.
/// </summary>
[Collection("real dictionary")]
public class AutocorrectBenchmarkTests
{
    // Before the previous word was used: 151 right, 12 wrong, 7 missed.
    // With it: 155 right, 6 wrong, 9 missed. Five of the six left are names
    // and loanwords at the start of a sentence, where there is no context.

    /// <summary>At least this many typos must come out right.</summary>
    private const int MinFixedRight = 155;

    /// <summary>At most this many cases may be changed into the wrong word.</summary>
    private const int MaxFixedWrong = 6;

    private readonly RealDictionary _dictionary;
    private readonly ITestOutputHelper _output;

    public AutocorrectBenchmarkTests(RealDictionary dictionary, ITestOutputHelper output)
    {
        _dictionary = dictionary;
        _output = output;
    }

    /// <summary>One line of the benchmark file.</summary>
    public sealed record Case(string? Previous, string Typed, string Expected)
    {
        public bool IsTypo => !string.Equals(Typed, Expected, StringComparison.Ordinal);
    }

    /// <summary>What happened to every case, pile by pile.</summary>
    public sealed record Score(int FixedRight, int FixedWrong, int Missed, int Kept, List<string> Wrong, List<string> Misses);

    [Fact]
    public void AutocorrectMeetsTheMeasuredBar()
    {
        var engine = new AutocorrectEngine(
            new FuzzyMatcher(_dictionary.Tree, _dictionary.Words),
            _dictionary.Words,
            users: null,
            bigrams: _dictionary.Bigrams);

        List<Case> cases = LoadCases();
        Score score = Run(engine, cases);

        Report(score, cases.Count);

        Assert.True(
            score.FixedWrong <= MaxFixedWrong,
            $"{score.FixedWrong} wrong fixes, the limit is {MaxFixedWrong}");
        Assert.True(
            score.FixedRight >= MinFixedRight,
            $"{score.FixedRight} typos fixed, at least {MinFixedRight} are needed");
    }

    // Runs every case through the engine. Time O(n) checks.
    public static Score Run(AutocorrectEngine engine, IEnumerable<Case> cases)
    {
        int right = 0, wrong = 0, missed = 0, kept = 0;
        var wrongList = new List<string>();
        var missList = new List<string>();

        foreach (Case c in cases)
        {
            bool sentenceStart = string.IsNullOrEmpty(c.Previous);
            AutocorrectResult result = engine.Check(c.Typed, sentenceStart, c.Previous);

            if (!result.Changed)
            {
                if (c.IsTypo)
                {
                    missed++;
                    missList.Add($"{c.Previous}|{c.Typed} ({result.Reason})");
                }
                else
                {
                    kept++;
                }

                continue;
            }

            if (string.Equals(result.Corrected, c.Expected, StringComparison.Ordinal))
            {
                right++;
            }
            else
            {
                wrong++;
                wrongList.Add($"{c.Previous}|{c.Typed} -> {result.Corrected} (wanted {c.Expected})");
            }
        }

        return new Score(right, wrong, missed, kept, wrongList, missList);
    }

    // Reads the benchmark file that sits next to the test assembly.
    public static List<Case> LoadCases()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Benchmarks", "autocorrect_cases.txt");
        var cases = new List<Case>();

        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] parts = line.Split('|');
            if (parts.Length != 3)
            {
                throw new FormatException($"Bad benchmark line: {line}");
            }

            cases.Add(new Case(parts[0].Length == 0 ? null : parts[0], parts[1], parts[2]));
        }

        return cases;
    }

    // Writes the piles to the test output, with every wrong fix and every miss
    // listed, so a change can be judged case by case.
    private void Report(Score score, int total)
    {
        _output.WriteLine($"cases {total}: fixed right {score.FixedRight}, fixed wrong {score.FixedWrong}, missed {score.Missed}, kept {score.Kept}");

        foreach (string line in score.Wrong)
        {
            _output.WriteLine("  WRONG  " + line);
        }

        foreach (string line in score.Misses)
        {
            _output.WriteLine("  MISSED " + line);
        }
    }
}
