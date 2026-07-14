namespace WDAS.Domain;

/// <summary>Stable IDs for seeded security roles (must not collide with seed user IDs).</summary>
public static class SecurityRoleIds
{
    public static readonly Guid SuperAdmin = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
    public static readonly Guid DepartmentAdmin = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02");
    public static readonly Guid MakerOwner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03");
    public static readonly Guid Approver = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04");
    public static readonly Guid Auditor = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05");
    public static readonly Guid ItAdmin = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06");

    public static Guid FromLegacyEnum(int enumValue) => enumValue switch
    {
        1 => SuperAdmin,
        2 => DepartmentAdmin,
        3 => MakerOwner,
        4 => Approver,
        5 => Auditor,
        6 => ItAdmin,
        _ => Guid.Empty,
    };
}
