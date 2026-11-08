using System.Globalization;
using System.Text;

namespace Pulse.Domain.Common;

public static class Slugs
{
    public const int MaxLength = 48;

    /// <summary>"Acme Platform!" becomes "acme-platform". Accents are dropped, other symbols become hyphens.</summary>
    public static string From(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingHyphen = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is >= 'a' and <= 'z' or >= '0' and <= '9' || char.IsAsciiLetterUpper(character))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingHyphen = false;
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                pendingHyphen = true;
            }
        }

        var slug = builder.ToString();
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        return slug.Length == 0 ? "team" : slug;
    }
}
