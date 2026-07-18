namespace WDAS.Infrastructure.Migrations;

/// <summary>
/// Lightweight preflight checks before applying UUID-to-integer identity migrations.
/// Call from ops scripts or migration runners; does not mutate data.
/// </summary>
public static class PreflightMigrationGuard
{
    public static void EnsurePositiveId(int id, string name)
    {
        if (id <= 0)
        {
            throw new InvalidOperationException($"{name} must be a positive integer identity value.");
        }
    }

    public static void EnsureOptionalPositiveId(int? id, string name)
    {
        if (id is <= 0)
        {
            throw new InvalidOperationException($"{name} must be null or a positive integer identity value.");
        }
    }

    public static void EnsureNoGuidPayload(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (Guid.TryParse(value, out _))
        {
            throw new InvalidOperationException(
                $"{name} still contains a GUID. Remap to integer identity before proceeding.");
        }
    }

    public static bool LooksLikeGuid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out _);
}
