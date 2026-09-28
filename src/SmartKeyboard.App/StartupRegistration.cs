using Microsoft.Win32;
using SmartKeyboard.Core;

namespace SmartKeyboard.App;

/// <summary>
/// Turns "Start with Windows" on and off.
///
/// It uses the per-user Run key, the same place any program's "start when I
/// sign in" option lives. It needs no Administrator rights and touches no one
/// else's account.
///
/// The key itself is the only record of the choice. It is not copied into
/// settings.txt, because two records can disagree, and then the tick in the
/// menu would say one thing while Windows did another. Reading the key every
/// time means the tick is always the truth, whether the choice was made here,
/// in Settings, or by install.cmd.
/// </summary>
public static class StartupRegistration
{
    /// <summary>Where Windows looks for programs to start at sign in.</summary>
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The name SmartKeyboard is listed under. install.cmd uses the same.</summary>
    public const string ValueName = "SmartKeyboard";

    /// <summary>True when Windows will start SmartKeyboard at sign in.</summary>
    // Time O(1), one registry read.
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is string command && command.Trim().Length > 0;
            }
            catch (Exception)
            {
                // Cannot read it, so do not claim it is on.
                return false;
            }
        }
    }

    // Starts this copy of SmartKeyboard, quietly, whenever the user signs in.
    // Returns false when Windows would not let us write the setting.
    // Time O(1), one registry write.
    public static bool Enable()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key.SetValue(ValueName, StartupCommand.Build(Application.ExecutablePath), RegistryValueKind.String);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Stops SmartKeyboard starting at sign in. Returns false when Windows
    // would not let us change the setting. Time O(1), one registry write.
    public static bool Disable()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Turns it on or off. Returns false when that could not be done.
    // Time O(1).
    public static bool Set(bool enabled)
    {
        return enabled ? Enable() : Disable();
    }
}
