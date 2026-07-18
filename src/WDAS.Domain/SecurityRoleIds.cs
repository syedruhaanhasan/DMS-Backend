namespace WDAS.Domain;

/// <summary>Stable codes for seeded security roles.</summary>
public static class SecurityRoleIds
{
    public const string SuperAdmin = "SuperAdmin";
    public const string DepartmentAdmin = "DepartmentAdmin";
    public const string MakerOwner = "MakerOwner";
    public const string Approver = "Approver";
    public const string Auditor = "Auditor";
    public const string ItAdmin = "ItAdmin";

    public static string FromLegacyEnum(int enumValue) => enumValue switch
    {
        1 => SuperAdmin,
        2 => DepartmentAdmin,
        3 => MakerOwner,
        4 => Approver,
        5 => Auditor,
        6 => ItAdmin,
        _ => string.Empty,
    };
}
