namespace WDAS.Application;

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string DepartmentAdmin = "DepartmentAdmin";
    public const string MakerOwner = "MakerOwner";
    public const string Approver = "Approver";
    public const string Auditor = "Auditor";
    public const string ItAdmin = "ItAdmin";

    /// <summary>Predefined, non-deletable role with configuration-only rights.</summary>
    public const string ConfigAdmin = "ConfigAdmin";
}
