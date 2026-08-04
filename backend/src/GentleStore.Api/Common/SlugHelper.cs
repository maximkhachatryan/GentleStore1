using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GentleStore.Api.Common;

public static partial class SlugHelper
{
    public static string Generate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Guid.NewGuid().ToString("n")[..8];

        var normalized = input.Trim().ToLowerInvariant();
        normalized = normalized.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var cleaned = NonAlphaNumeric().Replace(sb.ToString(), "-");
        cleaned = MultiDash().Replace(cleaned, "-").Trim('-');

        return string.IsNullOrEmpty(cleaned) ? Guid.NewGuid().ToString("n")[..8] : cleaned;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphaNumeric();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiDash();
}
