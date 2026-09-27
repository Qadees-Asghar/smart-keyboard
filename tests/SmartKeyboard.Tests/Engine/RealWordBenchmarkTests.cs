using SmartKeyboard.Core.Engine;
using Xunit.Abstractions;

namespace SmartKeyboard.Tests.Engine;

/// <summary>
/// Measures real-word fixing ("form" for "from") on the real word pair data.
///
/// Every case in Benchmarks/realword_cases.txt is a word with the words on
/// either side of it. Half are mixups that should be fixed, half are the
/// right word already and must be left alone. Fixed wrong is the number that
/// matters: here a wrong fix swaps one real word for another, which is much
/// harder to spot than a typo.
///
/// The limits are set from the last measured run, so a change that makes
/// things worse fails here.
/// </summary>
[Collection("real dictionary")]
public class RealWordBenchmarkTests
{
    // Measured: 33 of 37 mixups fixed, none of the 36 right words changed.
    // Before each side's say was capped it was 34 fixed but 1 wrong
    // ("made off with" became "made of with"), which is the worse trade.

    /// <summary>At least this many mixups must be fixed.</summary>
    private const int MinFixedRight = 33;

    /// <summary>At most this many cases may be changed wrongly.</summary>
    private const int MaxFixedWrong = 0;

    private readonly RealDictionary _dictionary;
    private readonly ITestOutputHelper _output;

    public RealWordBenchmarkTests(RealDictionary dictionary, ITestOutputHelper output)
    {
        _dictionary = dictionary;
        _output = output;
    }

    [Fact]
    public void RealWordFixingMeetsTheMeasuredBar()
    {
        var checker = new RealWordChecker(_dictionary.Bigrams, _dictionary.Words) { Enabled = true };

        int right = 0, wrong = 0, missed = 0, kept = 0;

        foreach ((string? before, string typed, string after, string expected) in LoadCases())
        {
            AutocorrectResult result = checker.Check(before, typed, after);
            bool isMixup = !string.Equals(typed, expected, StringComparison.Ordinal);
            string label = $"{before} [{typed}] {after}";

            if (!result.Changed)
            {
                if (isMixup)
                {
                    missed++;
                    _output.WriteLine($"  MISSED {label} ({result.Reason})");
                }
                else
                {
                    kept++;
                }

                continue;
            }

            if (string.Equals(result.Corrected, expected, StringComparison.Ordinal))
            {
                right++;
            }
            else
            {
                wrong++;
                _output.WriteLine($"  WRONG  {label} -> {result.Corrected} (wanted {expected})");
            }
        }

        _output.WriteLine($"fixed right {right}, fixed wrong {wrong}, missed {missed}, kept {kept}");

        Assert.True(wrong <= MaxFixedWrong, $"{wrong} wrong fixes, the limit is {MaxFixedWrong}");
        Assert.True(right >= MinFixedRight, $"{right} mixups fixed, at least {MinFixedRight} are needed");
    }

    // Reads the benchmark file that sits next to the test assembly.
    private static List<(string? Before, string Typed, string After, string Expected)> LoadCases()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Benchmarks", "realword_cases.txt");
        var cases = new List<(string?, string, string, string)>();

        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] parts = line.Split('|');
            if (parts.Length != 4)
            {
                throw new FormatException($"Bad benchmark line: {line}");
            }

            cases.Add((parts[0].Length == 0 ? null : parts[0], parts[1], parts[2], parts[3]));
        }

        return cases;
    }
}
