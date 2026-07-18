namespace WDAS.Application;

public static class IdParsing
{
    public static int ParseRequired(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out var id))
        {
            throw new InvalidOperationException($"{name} is missing or invalid.");
        }

        return id;
    }

    public static int? ParseOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, out var id) ? id : null;
    }

    public static string ToApi(int id) => id.ToString();
}
