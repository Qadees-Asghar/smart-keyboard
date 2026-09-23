namespace SmartKeyboard.Core.DataStructures;

/// <summary>
/// Levenshtein edit distance, written by hand with dynamic programming.
///
/// The distance is the smallest number of single letter changes needed to turn
/// one word into another. There are three allowed changes:
///   insert a letter    "cat" to "cart"   costs 1
///   delete a letter    "cart" to "cat"   costs 1
///   replace a letter   "cat" to "cut"    costs 1
///
/// The classic method fills a full table of size (n+1) by (m+1). Each cell only
/// ever looks at the row above it, so we keep two rows instead of the whole
/// table. That drops the memory from O(n*m) down to O(m).
/// </summary>
public static class EditDistance
{
    // Works out the distance between two words.
    // Time O(n*m). Space O(min(n, m)) because only two rows are kept.
    public static int Levenshtein(string? first, string? second)
    {
        string a = first ?? string.Empty;
        string b = second ?? string.Empty;

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        // Put the shorter word across the top so the rows stay small.
        if (a.Length < b.Length)
        {
            (a, b) = (b, a);
        }

        // previous holds the row above, current holds the row being filled.
        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];

        // Turning an empty string into the first j letters of b costs j inserts.
        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            // Turning the first i letters of a into an empty string costs i deletes.
            current[0] = i;

            for (int j = 1; j <= b.Length; j++)
            {
                int replaceCost = a[i - 1] == b[j - 1] ? 0 : 1;

                current[j] = Min3(
                    current[j - 1] + 1,             // insert a letter
                    previous[j] + 1,                // delete a letter
                    previous[j - 1] + replaceCost); // replace a letter
            }

            // The row we just filled becomes the row above for the next loop.
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    // Same idea, but gives up early once the distance is clearly too big.
    // This is much faster when we only care about small distances, which is
    // exactly what typo fixing needs.
    // Returns maxDistance + 1 to mean "further away than you asked for".
    // Time O(n*m) in the worst case, the same as the method above, but in
    // practice it stops after a few rows because the whole row goes over the
    // limit. That is what makes typo search fast.
    public static int LevenshteinWithLimit(string? first, string? second, int maxDistance)
    {
        string a = first ?? string.Empty;
        string b = second ?? string.Empty;

        if (maxDistance < 0)
        {
            maxDistance = 0;
        }

        // A length gap bigger than the limit cannot be closed. Stop now.
        if (Math.Abs(a.Length - b.Length) > maxDistance)
        {
            return maxDistance + 1;
        }

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        if (a.Length < b.Length)
        {
            (a, b) = (b, a);
        }

        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            int bestInRow = current[0];

            for (int j = 1; j <= b.Length; j++)
            {
                int replaceCost = a[i - 1] == b[j - 1] ? 0 : 1;

                current[j] = Min3(
                    current[j - 1] + 1,
                    previous[j] + 1,
                    previous[j - 1] + replaceCost);

                if (current[j] < bestInRow)
                {
                    bestInRow = current[j];
                }
            }

            // Every later row can only get bigger, so if the whole row is
            // already over the limit the answer is over the limit too.
            if (bestInRow > maxDistance)
            {
                return maxDistance + 1;
            }

            (previous, current) = (current, previous);
        }

        int result = previous[b.Length];
        return result > maxDistance ? maxDistance + 1 : result;
    }

    // Picks the smallest of three numbers. Time O(1).
    private static int Min3(int a, int b, int c)
    {
        int smallest = a < b ? a : b;
        return smallest < c ? smallest : c;
    }
}
