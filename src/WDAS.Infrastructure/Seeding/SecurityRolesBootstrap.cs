using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Domain;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Infrastructure.Persistence;

namespace WDAS.Infrastructure.Seeding;

public static class SecurityRolesBootstrap
{
    public static async Task EnsureSchemaAndSeedAsync(WdasDbContext db)
    {
        await EnsureTablesAsync(db);
        await MigrateLegacyRoleMappingsAsync(db);
        await SeedSystemRolesAsync(db);
    }

    private static async Task EnsureTablesAsync(WdasDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "SecurityRoles" (
                    "Id" uuid NOT NULL,
                    "Name" character varying(120) NOT NULL,
                    "Code" character varying(64) NOT NULL,
                    "Description" character varying(1000),
                    "IsSystem" boolean NOT NULL,
                    "IsActive" boolean NOT NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_SecurityRoles" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SecurityRoles_Code" ON "SecurityRoles" ("Code");

                CREATE TABLE IF NOT EXISTS "SecurityRolePermissions" (
                    "Id" uuid NOT NULL,
                    "RoleId" uuid NOT NULL,
                    "PermissionKey" character varying(120) NOT NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_SecurityRolePermissions" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_SecurityRolePermissions_SecurityRoles_RoleId"
                        FOREIGN KEY ("RoleId") REFERENCES "SecurityRoles" ("Id") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SecurityRolePermissions_RoleId_PermissionKey"
                    ON "SecurityRolePermissions" ("RoleId", "PermissionKey");
                """);
        }
        catch
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS SecurityRoles (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Code TEXT NOT NULL,
                    Description TEXT,
                    IsSystem INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SecurityRoles_Code ON SecurityRoles (Code);

                CREATE TABLE IF NOT EXISTS SecurityRolePermissions (
                    Id TEXT NOT NULL PRIMARY KEY,
                    RoleId TEXT NOT NULL,
                    PermissionKey TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT,
                    FOREIGN KEY (RoleId) REFERENCES SecurityRoles (Id) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SecurityRolePermissions_RoleId_PermissionKey
                    ON SecurityRolePermissions (RoleId, PermissionKey);
                """);
        }

        // Add RoleId column and migrate from legacy enum column "Role" when present.
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "RoleMappings" ADD COLUMN IF NOT EXISTS "RoleId" uuid;
                """);
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE RoleMappings ADD COLUMN RoleId TEXT");
            }
            catch
            {
                /* exists */
            }
        }
    }

    private static async Task MigrateLegacyRoleMappingsAsync(WdasDbContext db)
    {
        // Best-effort: copy legacy integer Role -> RoleId using known seed IDs.
        try
        {
            await db.Database.ExecuteSqlRawAsync($"""
                UPDATE "RoleMappings"
                SET "RoleId" = CASE "Role"
                    WHEN 1 THEN '{SecurityRoleIds.SuperAdmin}'
                    WHEN 2 THEN '{SecurityRoleIds.DepartmentAdmin}'
                    WHEN 3 THEN '{SecurityRoleIds.MakerOwner}'
                    WHEN 4 THEN '{SecurityRoleIds.Approver}'
                    WHEN 5 THEN '{SecurityRoleIds.Auditor}'
                    WHEN 6 THEN '{SecurityRoleIds.ItAdmin}'
                    ELSE "RoleId"
                END
                WHERE "RoleId" IS NULL AND "Role" IS NOT NULL;
                """);
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync($"""
                    UPDATE RoleMappings
                    SET RoleId = CASE Role
                        WHEN 1 THEN '{SecurityRoleIds.SuperAdmin}'
                        WHEN 2 THEN '{SecurityRoleIds.DepartmentAdmin}'
                        WHEN 3 THEN '{SecurityRoleIds.MakerOwner}'
                        WHEN 4 THEN '{SecurityRoleIds.Approver}'
                        WHEN 5 THEN '{SecurityRoleIds.Auditor}'
                        WHEN 6 THEN '{SecurityRoleIds.ItAdmin}'
                        ELSE RoleId
                    END
                    WHERE RoleId IS NULL AND Role IS NOT NULL;
                    """);
            }
            catch
            {
                /* Role column already dropped or provider mismatch */
            }
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "RoleMappings" DROP CONSTRAINT IF EXISTS "FK_RoleMappings_SecurityRoles_RoleId";
                """);
        }
        catch { /* ignore */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync($"""
                UPDATE "RoleMappings" SET "RoleId" = '{SecurityRoleIds.MakerOwner}' WHERE "RoleId" IS NULL;
                ALTER TABLE "RoleMappings" ALTER COLUMN "RoleId" SET NOT NULL;
                """);
        }
        catch { /* RoleId already NOT NULL */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_RoleMappings_UserId_RoleId_DepartmentId"
                    ON "RoleMappings" ("UserId", "RoleId", "DepartmentId");
                ALTER TABLE "RoleMappings"
                    ADD CONSTRAINT "FK_RoleMappings_SecurityRoles_RoleId"
                    FOREIGN KEY ("RoleId") REFERENCES "SecurityRoles" ("Id") ON DELETE RESTRICT;
                """);
        }
        catch { /* index/fk may already exist; FK may fail until roles seeded */ }

        // Drop legacy Role column after migration.
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                DROP INDEX IF EXISTS "IX_RoleMappings_UserId_Role_DepartmentId";
                ALTER TABLE "RoleMappings" DROP COLUMN IF EXISTS "Role";
                """);
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE RoleMappings DROP COLUMN Role");
            }
            catch { /* ignore */ }
        }
    }

    public static async Task SeedSystemRolesAsync(WdasDbContext db)
    {
        var now = DateTime.UtcNow;
        var defs = new (Guid Id, string Name, string Code, string Description)[]
        {
            (SecurityRoleIds.SuperAdmin, "Super Admin", RoleNames.SuperAdmin, "Full system administrator with configuration access."),
            (SecurityRoleIds.DepartmentAdmin, "Dept Admin", RoleNames.DepartmentAdmin, "Department-level oversight and reporting."),
            (SecurityRoleIds.MakerOwner, "Maker", RoleNames.MakerOwner, "Creates and owns documents through the approval cycle."),
            (SecurityRoleIds.Approver, "Approver", RoleNames.Approver, "Reviews and acts on assigned approval steps."),
            (SecurityRoleIds.Auditor, "Auditor", RoleNames.Auditor, "Read-only compliance and audit access."),
            (SecurityRoleIds.ItAdmin, "IT Admin", RoleNames.ItAdmin, "Directory and user administration."),
        };

        foreach (var def in defs)
        {
            var role = await db.SecurityRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == def.Id);
            if (role is null)
            {
                db.SecurityRoles.Add(new SecurityRole
                {
                    Id = def.Id,
                    Name = def.Name,
                    Code = def.Code,
                    Description = def.Description,
                    IsSystem = true,
                    IsActive = true,
                    CreatedAtUtc = now
                });
                await db.SaveChangesAsync();
            }
            else
            {
                await db.Database.ExecuteSqlAsync(
                    $"""
                    UPDATE "SecurityRoles"
                    SET "Name" = {def.Name},
                        "Code" = {def.Code},
                        "Description" = {def.Description},
                        "IsSystem" = TRUE,
                        "IsActive" = TRUE,
                        "UpdatedAtUtc" = {now}
                    WHERE "Id" = {def.Id}
                    """);
            }

            var desired = PermissionCatalog.ForLegacyRole(def.Code).ToHashSet(StringComparer.Ordinal);
            if (def.Code == RoleNames.SuperAdmin)
            {
                foreach (var key in PermissionCatalog.AllKeys)
                {
                    desired.Add(key);
                }
            }

            foreach (var key in desired)
            {
                var permissionId = Guid.NewGuid();
                await db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO "SecurityRolePermissions" ("Id", "RoleId", "PermissionKey", "CreatedAtUtc")
                    SELECT {permissionId}, {def.Id}, {key}, {now}
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "SecurityRolePermissions"
                        WHERE "RoleId" = {def.Id} AND "PermissionKey" = {key})
                    """);
            }
        }

        // Re-try FK after roles exist
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "RoleMappings"
                    ADD CONSTRAINT "FK_RoleMappings_SecurityRoles_RoleId"
                    FOREIGN KEY ("RoleId") REFERENCES "SecurityRoles" ("Id") ON DELETE RESTRICT;
                """);
        }
        catch { /* already exists */ }
    }

    public static Guid RoleIdForLegacy(ApplicationRole role) =>
        SecurityRoleIds.FromLegacyEnum((int)role);
}
