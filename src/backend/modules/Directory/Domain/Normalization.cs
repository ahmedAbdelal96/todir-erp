using System.Globalization;
using System.Text;

namespace Todir.Modules.Directory.Domain;

public static class DirectoryNormalization
{
    public static string Name(string? value) => Normalize(value, true);

    public static string Email(string value)
    {
        var trimmed = value.Trim();
        var at = trimmed.LastIndexOf('@');
        return at <= 0 ? trimmed.ToLowerInvariant() : trimmed[..at].ToLowerInvariant() + trimmed[at..].ToLowerInvariant();
    }

    public static string Phone(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormKC))
        {
            if (character is '+') builder.Append(character);
            else if (char.IsDigit(character)) builder.Append(ToWesternDigit(character));
        }
        return builder.ToString();
    }

    public static string Identifier(string value) => Normalize(value, false).Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string Normalize(string? value, bool removeDiacritics)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().Normalize(NormalizationForm.FormD).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (removeDiacritics && CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                'ؤ' => 'و',
                'ئ' => 'ي',
                'ـ' => '\0',
                _ => ToWesternDigit(character)
            });
        }
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static char ToWesternDigit(char character) => character switch
    {
        >= '٠' and <= '٩' => (char)('0' + character - '٠'),
        >= '۰' and <= '۹' => (char)('0' + character - '۰'),
        _ => character
    };
}
