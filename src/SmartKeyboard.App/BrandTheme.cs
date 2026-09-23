using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace SmartKeyboard.App;

/// <summary>
/// The look of the app: Anthropic's brand colours and fonts.
///
/// Poppins is used for the parts of the window you do not type in (menu,
/// status bar, buttons, the suggestion list). Lora is used for the text you
/// actually write.
///
/// Neither font is installed on a normal Windows PC, so both ship with the
/// program in Assets/Fonts and are loaded at startup. Nothing is installed
/// onto the machine. If loading ever fails, Arial and Georgia are used
/// instead, which are on every Windows PC.
/// </summary>
public static class BrandTheme
{
    // Main colours.
    public static readonly Color Dark = FromHex("#141413");
    public static readonly Color Light = FromHex("#faf9f5");
    public static readonly Color MidGray = FromHex("#b0aea5");
    public static readonly Color LightGray = FromHex("#e8e6dc");

    // Accent colours.
    public static readonly Color Orange = FromHex("#d97757");
    public static readonly Color Blue = FromHex("#6a9bcc");
    public static readonly Color Green = FromHex("#788c5d");

    /// <summary>A misspelled word. Red is a signal, so it stays red.</summary>
    public static readonly Color Misspelled = Color.FromArgb(200, 40, 40);

    // The collection has to stay alive for the whole run, or the fonts stop
    // working the moment it is collected.
    private static readonly PrivateFontCollection Fonts = new();

    private const int FR_PRIVATE = 0x10;

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string path, int flags, IntPtr reserved);

    /// <summary>The font for menus, buttons and lists. Poppins, or Arial.</summary>
    public static FontFamily UiFamily { get; private set; } = FontFamily.GenericSansSerif;

    /// <summary>The font for the text being written. Lora, or Georgia.</summary>
    public static FontFamily TextFamily { get; private set; } = FontFamily.GenericSerif;

    /// <summary>True when the real brand fonts were found and loaded.</summary>
    public static bool UsingBrandFonts { get; private set; }

    // Loads the fonts that ship with the program. Call this once at startup,
    // before any window is built.
    // Time O(number of font files).
    public static void Load()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");

        string[] files =
        {
            "Poppins-Regular.ttf",
            "Poppins-Medium.ttf",
            "Poppins-SemiBold.ttf",
            "Lora-Variable.ttf",
        };

        foreach (string file in files)
        {
            string path = Path.Combine(folder, file);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                // GDI+ draws with this one.
                Fonts.AddFontFile(path);

                // Windows Forms often draws text with plain GDI instead, and
                // that does not look inside the collection above. Registering
                // the file privately lets both find the font.
                AddFontResourceEx(path, FR_PRIVATE, IntPtr.Zero);
            }
            catch (FileNotFoundException)
            {
                // A missing font is not worth stopping the program for.
            }
        }

        UiFamily = Find("Poppins") ?? SystemFamily("Arial") ?? FontFamily.GenericSansSerif;
        TextFamily = Find("Lora") ?? SystemFamily("Georgia") ?? FontFamily.GenericSerif;

        UsingBrandFonts = UiFamily.Name == "Poppins" && TextFamily.Name == "Lora";
    }

    /// <summary>Builds a font for menus, buttons and lists.</summary>
    // Time O(1).
    public static Font Ui(float size, FontStyle style = FontStyle.Regular)
    {
        return Safe(UiFamily, size, style);
    }

    /// <summary>Builds a font for the text being written.</summary>
    // Time O(1).
    public static Font Text(float size, FontStyle style = FontStyle.Regular)
    {
        return Safe(TextFamily, size, style);
    }

    // Some fonts do not carry every style. Asking for one they do not have
    // throws, so fall back to Regular rather than crashing the window.
    // Time O(1).
    private static Font Safe(FontFamily family, float size, FontStyle style)
    {
        if (!family.IsStyleAvailable(style))
        {
            style = family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : style;
        }

        try
        {
            return new Font(family, size, style);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, size);
        }
    }

    // Looks for a family among the fonts that shipped with the program.
    // Time O(N) over the loaded families.
    private static FontFamily? Find(string name)
    {
        foreach (FontFamily family in Fonts.Families)
        {
            if (family.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return family;
            }
        }

        return null;
    }

    // Looks for a family already installed on the PC.
    // Time O(N) over the installed families.
    private static FontFamily? SystemFamily(string name)
    {
        try
        {
            return new FontFamily(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Turns "#rrggbb" into a colour. Time O(1).
    private static Color FromHex(string hex)
    {
        return ColorTranslator.FromHtml(hex);
    }
}
