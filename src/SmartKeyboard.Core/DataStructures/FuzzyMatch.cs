namespace SmartKeyboard.Core.DataStructures;

/// <summary>
/// A real word that is close to a typo, and how many edits away it is.
///
/// Edit distance counts every kind of slip as costing the same, and real
/// fingers do not work that way. So each match also records which kind of
/// slip would explain it, and <see cref="SlipRank"/> puts those kinds in
/// order of how likely they are. That order decides which of several words
/// the same distance away is offered first.
/// </summary>
public sealed class FuzzyMatch
{
    public FuzzyMatch(
        string word,
        int distance,
        int frequency = 0,
        bool isMissingApostrophe = false,
        bool isDoubleLetterFix = false,
        bool isLetterSwap = false,
        bool isCompletion = false)
    {
        Word = word;
        Distance = distance;
        Frequency = frequency;
        IsMissingApostrophe = isMissingApostrophe;
        IsDoubleLetterFix = isDoubleLetterFix;
        IsLetterSwap = isLetterSwap;
        IsCompletion = isCompletion;
    }

    /// <summary>The real word from the dictionary.</summary>
    public string Word { get; }

    /// <summary>How many single letter changes away it is. Smaller is better.</summary>
    public int Distance { get; }

    /// <summary>How common the word is. Used to break ties between same distance words.</summary>
    public int Frequency { get; }

    /// <summary>
    /// True when this word is the typo with an apostrophe put back, like
    /// "can't" for "cant". Every letter was typed correctly and in the right
    /// order, and only a punctuation mark is missing. People leave the
    /// apostrophe out more than they make any other mistake, often on
    /// purpose, so this is the likeliest explanation of all.
    /// </summary>
    public bool IsMissingApostrophe { get; }

    /// <summary>
    /// True when this word differs from the typo only by a doubled letter,
    /// like "hello" for "helo" or "until" for "untill". The two letters are
    /// the same key pressed twice, and only one of them registering is a very
    /// common slip.
    /// </summary>
    public bool IsDoubleLetterFix { get; }

    /// <summary>
    /// True when two neighbouring letters are the wrong way round, like "the"
    /// for "teh" or "are" for "aer". Both keys were pressed, just in the wrong
    /// order, which is the commonest typing mistake there is.
    /// </summary>
    public bool IsLetterSwap { get; }

    /// <summary>
    /// True when the typo is the start of this word, so every letter that was
    /// actually typed is kept and only the ending is missing. "hav" is the
    /// start of "have"; "has" and "had" are not, they claim the v that was
    /// pressed was a mistake.
    /// </summary>
    public bool IsCompletion { get; }

    /// <summary>
    /// Which kind of slip this would be, lower being more likely. Every key
    /// the user pressed is evidence: the kinds that keep all of them come
    /// first, and changing a letter, which throws one away, comes last.
    /// </summary>
    public int SlipRank =>
        IsMissingApostrophe ? 0
        : IsDoubleLetterFix ? 1
        : IsLetterSwap ? 2
        : IsCompletion ? 3
        : 4;

    /// <summary>Copies this match with the frequency filled in.</summary>
    public FuzzyMatch WithFrequency(int frequency) =>
        new(Word, Distance, frequency, IsMissingApostrophe, IsDoubleLetterFix, IsLetterSwap, IsCompletion);

    public override string ToString() => $"{Word} (distance {Distance}, count {Frequency})";
}
