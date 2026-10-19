using System.Globalization;

namespace BAModManager.Core;

/// <summary>
/// Translations. Every piece of UI text goes through T("English text"); the English text is the key,
/// and missing translations fall back to English. Placeholders use {0}, {1}… so each language can
/// order words its own way. The table itself is in Translations.cs.
/// </summary>
public static partial class Loc
{
    public const string RestartArg = "--restart";

    /// <summary>Languages offered in Settings: code and name (in that language).</summary>
    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"),
        ("de", "Deutsch"),
        ("fr", "Français"),
        ("ru", "Русский"),
        ("zh", "中文 (简体)"),
    };

    public static string Current { get; private set; } = "en";

    public static bool IsChinese => Current == "zh";

    // Column in the translation table
    private static int Column => Current switch { "de" => 0, "fr" => 1, "ru" => 2, "zh" => 3, _ => -1 };

    /// <summary>Picks the language; null follows the Windows display language (English if it isn't offered).</summary>
    public static void SetLanguage(string? code)
    {
        code ??= CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Current = Languages.Any(l => l.Code == code) ? code : "en";
    }

    public static string T(string english)
    {
        int column = Column;
        if (column < 0) return english;
        return Table.TryGetValue(english, out var row) && column < row.Length && !string.IsNullOrEmpty(row[column])
            ? row[column]
            : english;
    }

    public static string T(string english, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, T(english), args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, english, args);
        }
    }
}
