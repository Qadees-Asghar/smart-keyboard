namespace SmartKeyboard.Core.Engine;

/// <summary>
/// Decides whether a window title suggests a password is being typed.
///
/// A program that sees every key on the PC must stop watching when a password
/// is on screen. Checking the focused control catches standard Windows
/// password boxes, but it cannot see inside a browser, so the window title is
/// the second line of defence.
///
/// This is plain string matching with no Windows in it, so it lives in Core
/// and is properly tested. Getting this wrong is a privacy problem, not a
/// cosmetic one, so it errs towards stopping.
/// </summary>
public static class SensitiveWindowDetector
{
    /// <summary>Words in a window title that mean "stop watching".</summary>
    public static readonly string[] SensitiveWords =
    {
        "password",
        "passcode",
        "passphrase",
        "sign in",
        "signin",
        "sign-in",
        "log in",
        "login",
        "log-in",
        "credential",
        "authentication",
        "authenticator",
        "two factor",
        "2fa",
        "bitlocker",
        "keychain",
        "1password",
        "bitwarden",
        "lastpass",
        "keepass",
        "dashlane",
        "private browsing",
        "incognito",
        "seed phrase",
        "recovery phrase",
    };

    // True when the title suggests something private is being typed.
    // Time O(T * W) where T is the title length and W the number of words,
    // which is tiny and runs once per keystroke at most.
    public static bool LooksSensitive(string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return false;
        }

        string lower = windowTitle.ToLowerInvariant();

        foreach (string word in SensitiveWords)
        {
            if (lower.Contains(word, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
