using System.Text.RegularExpressions;

namespace WDAS.Domain.Common;

public static partial class EmailFormatValidator
{
    private static readonly Regex Pattern = EmailRegex();

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return Pattern.IsMatch(email.Trim());
    }

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
