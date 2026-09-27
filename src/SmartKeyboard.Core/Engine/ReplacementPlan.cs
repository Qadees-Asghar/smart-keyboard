namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Works out the shortest way to turn what was typed into what is wanted.
///
/// SmartKeyboard cannot edit another app's text. All it can do is send
/// Backspace to rub letters out and then send the new ones. The old way was to
/// rub out the whole word and type the replacement, which is simple but sends
/// far more keys than it needs to.
///
/// That matters because the backspaces are the part that goes wrong. A modern
/// app such as the Store version of Notepad, or anything built on Chromium,
/// does not act on a key the moment it arrives: it hands it to another thread
/// first. Fire a dozen keys at it with no gap and some of them get lost. When
/// none of the backspaces land you get "helohello". When some of them do you
/// get "hhello". When one too many lands a letter goes missing.
///
/// So the fix is to send fewer. Letters that are already correct are left
/// alone, and only the tail that differs is replaced:
///
///     typed "helo "  wanted "hello "   keep "hel", rub out 2, type "lo "
///     typed "cant "  wanted "can't "   keep "can", rub out 2, type "'t "
///     typed "wor"    wanted "world "   keep "wor", rub out 0, type "ld "
///
/// That last one is the important case. Accepting a suggestion is usually
/// finishing a word you already started, so it needs no backspaces at all, and
/// a key that is never sent cannot be dropped.
///
/// The comparison is case sensitive on purpose. "teh" to "The" shares nothing,
/// because "t" and "T" are different, so it correctly falls back to replacing
/// the whole word. Comparing loosely would keep the lower case "t" and leave
/// "the" behind.
/// </summary>
public readonly struct ReplacementPlan
{
    private ReplacementPlan(int backspaces, string toType)
    {
        Backspaces = backspaces;
        ToType = toType;
    }

    /// <summary>How many letters to rub out from the end of what was typed.</summary>
    public int Backspaces { get; }

    /// <summary>What to type once they are gone. May be empty.</summary>
    public string ToType { get; }

    /// <summary>True when the text is already right and nothing need be sent.</summary>
    public bool IsNothingToDo => Backspaces == 0 && ToType.Length == 0;

    // Builds the plan. Time O(L) over the shorter of the two strings.
    public static ReplacementPlan Compute(string? typed, string? replacement)
    {
        string from = typed ?? string.Empty;
        string to = replacement ?? string.Empty;

        int shared = SharedStart(from, to);

        return new ReplacementPlan(from.Length - shared, to[shared..]);
    }

    // Counts the letters the two strings begin with in common.
    // Time O(L) over the shorter one.
    private static int SharedStart(string from, string to)
    {
        int limit = Math.Min(from.Length, to.Length);
        int i = 0;

        while (i < limit && from[i] == to[i])
        {
            i++;
        }

        return i;
    }
}
