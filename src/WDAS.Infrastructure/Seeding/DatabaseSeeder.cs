using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Infrastructure.Identity;
using WDAS.Infrastructure.Persistence;

namespace WDAS.Infrastructure.Seeding;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WdasDbContext>();
        var configuration = scope.ServiceProvider.GetService<IConfiguration>();

        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (!db.Database.IsNpgsql())
        {
            // Non-PostgreSQL providers (e.g. SQLite in tests) may fall back to EnsureCreated.
            await db.Database.EnsureCreatedAsync();
            _ = ex;
        }

        // InMemory (integration tests): schema comes from EnsureCreated; skip relational DDL patches.
        if (db.Database.IsRelational())
        {
            await EnsureWorkflowVersionColumnsAsync(db);
            await EnsureDocumentRevisionColumnAsync(db);
            await EnsureDocumentTypesTableAsync(db);
            await EnsureDocumentTypeColumnsAsync(db);
            await EnsureActiveDirectorySettingsTableAsync(db);
            await EnsureRevokedTokensTableAsync(db);
            await EnsureDocumentRecipientReviewerColumnsAsync(db);
            await EnsureUserTypesTableAsync(db);
            await AddNullableIntColumnAsync(db, "Users", "UserTypeId");
            await AddNullableTimestampColumnAsync(db, "WorkflowSteps", "SeenByApproverAtUtc");
        }

        await SecurityRolesBootstrap.EnsureSchemaAndSeedAsync(db);
        await EnsureActiveWorkflowVersionsAsync(db);
        await EnsureDocumentTypesAsync(db);
        await EnsureUserTypesAsync(db);
        await EnsureActiveDirectorySettingsAsync(db, configuration);

        if (await db.Users.AnyAsync())
        {
            await EnsureDevUsersAsync(db);
            return;
        }

        await SeedFullDatasetAsync(db);
    }

    private static async Task EnsureDevUsersAsync(WdasDbContext db)
    {
        var now = DateTime.UtcNow;
        var departmentMap = await EnsureDepartmentsAsync(db, now);

        foreach (var devUser in DevIdentityProvider.GetSeedUsers())
        {
            devUser.DepartmentId = departmentMap[devUser.DepartmentAdObjectId].Id;

            var existing = await db.Users.FirstOrDefaultAsync(u => u.AdObjectId == devUser.AdObjectId);
            if (existing is not null)
            {
                await EnsureDevUserRoleAsync(db, existing.Id, devUser.Role, devUser.DepartmentId, now);
                continue;
            }

            var user = new User
            {
                AdObjectId = devUser.AdObjectId,
                UserPrincipalName = devUser.UserPrincipalName,
                DisplayName = devUser.DisplayName,
                Email = devUser.Email,
                Title = devUser.Title,
                DepartmentId = devUser.DepartmentId,
                IsEnabledInAd = true,
                CreatedAtUtc = now,
                LastSyncedAtUtc = now
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.RoleMappings.Add(new RoleMapping
            {
                UserId = user.Id,
                RoleId = await SecurityRolesBootstrap.RoleIdForLegacyAsync(db, devUser.Role),
                DepartmentId = devUser.Role == ApplicationRole.SuperAdmin ? null : devUser.DepartmentId,
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureDevUserRoleAsync(
        WdasDbContext db,
        int userId,
        ApplicationRole role,
        int departmentId,
        DateTime now)
    {
        var roleId = await SecurityRolesBootstrap.RoleIdForLegacyAsync(db, role);
        var hasRole = await db.RoleMappings.AnyAsync(r => r.UserId == userId && r.RoleId == roleId);
        if (hasRole)
        {
            return;
        }

        db.RoleMappings.Add(new RoleMapping
        {
            UserId = userId,
            RoleId = roleId,
            DepartmentId = role == ApplicationRole.SuperAdmin ? null : departmentId,
            CreatedAtUtc = now
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedFullDatasetAsync(WdasDbContext db)
    {
        var now = DateTime.UtcNow;
        var departmentMap = await EnsureDepartmentsAsync(db, now);

        foreach (var devUser in DevIdentityProvider.GetSeedUsers())
        {
            devUser.DepartmentId = departmentMap[devUser.DepartmentAdObjectId].Id;

            var user = new User
            {
                AdObjectId = devUser.AdObjectId,
                UserPrincipalName = devUser.UserPrincipalName,
                DisplayName = devUser.DisplayName,
                Email = devUser.Email,
                Title = devUser.Title,
                DepartmentId = devUser.DepartmentId,
                IsEnabledInAd = true,
                CreatedAtUtc = now,
                LastSyncedAtUtc = now
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.RoleMappings.Add(new RoleMapping
            {
                UserId = user.Id,
                RoleId = await SecurityRolesBootstrap.RoleIdForLegacyAsync(db, devUser.Role),
                DepartmentId = devUser.Role == ApplicationRole.SuperAdmin ? null : devUser.DepartmentId,
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync();

        var seededUsers = await db.Users
            .Where(u => u.UserPrincipalName == "approver.one" || u.UserPrincipalName == "approver.two")
            .ToDictionaryAsync(u => u.UserPrincipalName, StringComparer.OrdinalIgnoreCase);
        var approver1UserId = seededUsers["approver.one"].Id;
        var approver2UserId = seededUsers["approver.two"].Id;

        var financeDept = departmentMap["ad-fin"];

        var workflow = new Workflow
        {
            DepartmentId = financeDept.Id,
            Name = "Purchase Request",
            DocumentType = "PurchaseRequest",
            Description = "Finance purchase request approval workflow",
            IsActive = true,
            CreatedAtUtc = now
        };

        var version = new WorkflowVersion
        {
            WorkflowId = workflow.Id,
            VersionNumber = 1,
            State = WorkflowVersionState.Active,
            ApprovalMode = ApprovalMode.Group,
            ReturnResumePolicy = ReturnResumePolicy.RestartFromFirst,
            SlaThresholdHours = 48,
            EscalationEnabled = true,
            ActivatedAtUtc = now,
            CreatedAtUtc = now
        };

        var group1 = new ApproverGroup
        {
            WorkflowVersionId = version.Id,
            Name = "Line Manager",
            SequenceOrder = 1,
            Requirement = GroupApprovalRequirement.AnyOneMember,
            CreatedAtUtc = now,
            Members =
            [
                new ApproverGroupMember { UserId = approver1UserId, CreatedAtUtc = now }
            ]
        };

        var group2 = new ApproverGroup
        {
            WorkflowVersionId = version.Id,
            Name = "Finance Director",
            SequenceOrder = 2,
            Requirement = GroupApprovalRequirement.AnyOneMember,
            CreatedAtUtc = now,
            Members =
            [
                new ApproverGroupMember { UserId = approver2UserId, CreatedAtUtc = now }
            ]
        };

        version.ApproverGroups.Add(group1);
        version.ApproverGroups.Add(group2);
        workflow.Versions.Add(version);
        db.Workflows.Add(workflow);

        var matrixWorkflow = new Workflow
        {
            DepartmentId = financeDept.Id,
            Name = "Capital Expenditure",
            DocumentType = "CapEx",
            Description = "Amount-based matrix workflow",
            IsActive = true,
            CreatedAtUtc = now
        };

        var matrixVersion = new WorkflowVersion
        {
            WorkflowId = matrixWorkflow.Id,
            VersionNumber = 1,
            State = WorkflowVersionState.Active,
            ApprovalMode = ApprovalMode.Matrix,
            ReturnResumePolicy = ReturnResumePolicy.RestartFromFirst,
            SlaThresholdHours = 24,
            EscalationEnabled = true,
            ActivatedAtUtc = now,
            CreatedAtUtc = now,
            MatrixTiers =
            [
                new ApprovalMatrixTier
                {
                    SequenceOrder = 1,
                    MinAmount = 0m,
                    MaxAmount = 10000m,
                    ApproverUserIdsJson = JsonSerializer.Serialize(new[] { approver1UserId }),
                    CreatedAtUtc = now
                },
                new ApprovalMatrixTier
                {
                    SequenceOrder = 2,
                    MinAmount = 10000.01m,
                    MaxAmount = null,
                    ApproverUserIdsJson = JsonSerializer.Serialize(new[] { approver1UserId, approver2UserId }),
                    CreatedAtUtc = now
                }
            ]
        };

        matrixWorkflow.Versions.Add(matrixVersion);
        db.Workflows.Add(matrixWorkflow);

        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, Department>> EnsureDepartmentsAsync(WdasDbContext db, DateTime now)
    {
        foreach (var adId in DevIdentityProvider.GetSeedUsers().Select(u => u.DepartmentAdObjectId).Distinct())
        {
            var sample = DevIdentityProvider.GetSeedUsers().First(u => u.DepartmentAdObjectId == adId);
            var existing = await db.Departments.FirstOrDefaultAsync(d => d.AdObjectId == adId);
            if (existing is not null)
            {
                continue;
            }

            db.Departments.Add(new Department
            {
                AdObjectId = adId,
                Name = sample.DepartmentCode == "FIN" ? "Finance" : "Information Technology",
                Code = sample.DepartmentCode,
                IsActive = true,
                CreatedAtUtc = now,
                LastSyncedAtUtc = now
            });
        }

        await db.SaveChangesAsync();

        var departments = await db.Departments
            .Where(d => d.AdObjectId == "ad-it" || d.AdObjectId == "ad-fin")
            .ToListAsync();

        return departments.ToDictionary(d => d.AdObjectId);
    }

    /// <summary>
    /// Activates draft workflow versions when no active version exists, so document creation works.
    /// </summary>
    private static async Task EnsureActiveWorkflowVersionsAsync(WdasDbContext db)
    {
        var now = DateTime.UtcNow;
        var workflows = await db.Workflows
            .Include(w => w.Versions)
            .Where(w => w.IsActive)
            .ToListAsync();

        var changed = false;
        foreach (var workflow in workflows)
        {
            if (workflow.Versions.Any(v => v.State == WorkflowVersionState.Active))
            {
                continue;
            }

            var latest = workflow.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            if (latest is null || latest.State != WorkflowVersionState.Draft)
            {
                continue;
            }

            latest.State = WorkflowVersionState.Active;
            latest.ActivatedAtUtc ??= now;
            latest.UpdatedAtUtc = now;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureDocumentTypesTableAsync(WdasDbContext db)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS "DocumentTypeDefinitions" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "Name" character varying(256) NOT NULL,
                "Code" character varying(64) NOT NULL,
                "Description" character varying(1000),
                "Category" character varying(32) NOT NULL,
                "IsActive" boolean NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone,
                CONSTRAINT "PK_DocumentTypeDefinitions" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentTypeDefinitions_Code"
                ON "DocumentTypeDefinitions" ("Code");
            """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch
        {
            // SQLite / alternate providers use compatible DDL without quoted identifiers.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS DocumentTypeDefinitions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Code TEXT NOT NULL,
                    Description TEXT,
                    Category TEXT NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_DocumentTypeDefinitions_Code
                    ON DocumentTypeDefinitions (Code);
                """);
        }
    }

    private static async Task EnsureActiveDirectorySettingsTableAsync(WdasDbContext db)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS "ActiveDirectorySettings" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "Enabled" boolean NOT NULL,
                "DomainName" character varying(256) NOT NULL,
                "Port" integer NOT NULL,
                "UseSsl" boolean NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone,
                CONSTRAINT "PK_ActiveDirectorySettings" PRIMARY KEY ("Id")
            );
            """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch
        {
            // SQLite / alternate providers use compatible DDL without quoted identifiers.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS ActiveDirectorySettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Enabled INTEGER NOT NULL,
                    DomainName TEXT NOT NULL,
                    Port INTEGER NOT NULL,
                    UseSsl INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT
                );
                """);
        }
    }

    private static async Task EnsureDocumentRecipientReviewerColumnsAsync(WdasDbContext db)
    {
        await AddNullableIntColumnAsync(db, "DocumentRecipients", "ReviewerUserId");
        await AddNullableIntColumnAsync(db, "DocumentRecipients", "AddedByUserId");
    }

    private static async Task AddNullableIntColumnAsync(WdasDbContext db, string table, string column)
    {
        // Column/table names are fixed literals from the caller (no user input).
        var pgSql = $"ALTER TABLE \"{table}\" ADD COLUMN IF NOT EXISTS \"{column}\" integer;";
        var sqliteSql = $"ALTER TABLE {table} ADD COLUMN {column} INTEGER";
        try
        {
            await db.Database.ExecuteSqlRawAsync(pgSql);
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sqliteSql);
            }
            catch
            {
                /* column may already exist */
            }
        }
    }

    private static async Task AddNullableTimestampColumnAsync(WdasDbContext db, string table, string column)
    {
        // Column/table names are fixed literals from the caller (no user input).
        var pgSql = $"ALTER TABLE \"{table}\" ADD COLUMN IF NOT EXISTS \"{column}\" timestamp with time zone;";
        var sqliteSql = $"ALTER TABLE {table} ADD COLUMN {column} TEXT";
        try
        {
            await db.Database.ExecuteSqlRawAsync(pgSql);
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sqliteSql);
            }
            catch
            {
                /* column may already exist */
            }
        }
    }

    private static async Task EnsureRevokedTokensTableAsync(WdasDbContext db)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS "RevokedTokens" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "Jti" character varying(64) NOT NULL,
                "ExpiresAtUtc" timestamp with time zone NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone,
                CONSTRAINT "PK_RevokedTokens" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RevokedTokens_Jti" ON "RevokedTokens" ("Jti");
            """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch
        {
            // SQLite / alternate providers use compatible DDL without quoted identifiers.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS RevokedTokens (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Jti TEXT NOT NULL,
                    ExpiresAtUtc TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT
                );
                """);
            await db.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_RevokedTokens_Jti ON RevokedTokens (Jti);");
        }
    }

    /// <summary>
    /// Seeds the single AD settings row on first run, carrying over any existing
    /// values from the legacy "Ldap" appsettings section so nothing is lost.
    /// </summary>
    private static async Task EnsureActiveDirectorySettingsAsync(WdasDbContext db, IConfiguration? configuration)
    {
        if (await db.ActiveDirectorySettings.AnyAsync())
        {
            return;
        }

        var ldap = configuration?.GetSection("Ldap");
        var enabled = ldap?.GetValue("Enabled", false) ?? false;
        var domainName = ldap?.GetValue<string>("Host") ?? string.Empty;
        var port = ldap?.GetValue("Port", 389) ?? 389;
        var useSsl = ldap?.GetValue("UseSsl", false) ?? false;

        db.ActiveDirectorySettings.Add(new ActiveDirectorySetting
        {
            Enabled = enabled,
            DomainName = domainName,
            Port = port is < 1 or > 65535 ? 389 : port,
            UseSsl = useSsl,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    private static async Task EnsureDocumentTypeColumnsAsync(WdasDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "DocumentTypeDefinitions"
                ADD COLUMN IF NOT EXISTS "AmountRequired" boolean NOT NULL DEFAULT FALSE;
                UPDATE "DocumentTypeDefinitions"
                SET "AmountRequired" = TRUE
                WHERE "Category" = 'financial' AND "AmountRequired" = FALSE;
                """);
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE DocumentTypeDefinitions ADD COLUMN AmountRequired INTEGER NOT NULL DEFAULT 0");
            }
            catch
            {
                /* column may already exist */
            }
        }
    }

    private static async Task EnsureWorkflowVersionColumnsAsync(WdasDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "WorkflowVersions"
                ADD COLUMN IF NOT EXISTS "ApprovalSequence" integer NOT NULL DEFAULT 0;
                """);
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE WorkflowVersions ADD COLUMN ApprovalSequence INTEGER NOT NULL DEFAULT 0");
            }
            catch
            {
                /* column may already exist */
            }
        }
    }

    private static async Task EnsureDocumentRevisionColumnAsync(WdasDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Documents"
                ADD COLUMN IF NOT EXISTS "RevisionNumber" integer NOT NULL DEFAULT 1;
                """);
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN RevisionNumber INTEGER NOT NULL DEFAULT 1");
            }
            catch
            {
                /* column may already exist */
            }
        }
    }

    private static async Task EnsureDocumentTypesAsync(WdasDbContext db)
    {
        if (await db.DocumentTypeDefinitions.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        var workflowTypes = await db.Workflows
            .AsNoTracking()
            .Select(w => new { w.DocumentType, w.Name, w.Description })
            .ToListAsync();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in workflowTypes)
        {
            var code = string.IsNullOrWhiteSpace(item.DocumentType)
                ? item.Name.Replace(" ", "", StringComparison.Ordinal)
                : item.DocumentType.Trim();

            if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
            {
                continue;
            }

            var label = code + item.Name;
            var financial = label.Contains("purchase", StringComparison.OrdinalIgnoreCase)
                || label.Contains("financial", StringComparison.OrdinalIgnoreCase)
                || label.Contains("invoice", StringComparison.OrdinalIgnoreCase)
                || label.Contains("payment", StringComparison.OrdinalIgnoreCase)
                || label.Contains("expense", StringComparison.OrdinalIgnoreCase)
                || label.Contains("capex", StringComparison.OrdinalIgnoreCase);
            db.DocumentTypeDefinitions.Add(new DocumentTypeDefinition
            {
                Name = string.IsNullOrWhiteSpace(item.DocumentType) ? item.Name : item.DocumentType,
                Code = code,
                Description = item.Description,
                Category = financial ? "financial" : "non_financial",
                AmountRequired = financial,
                IsActive = true,
                CreatedAtUtc = now,
            });
        }

        if (seen.Count == 0)
        {
            db.DocumentTypeDefinitions.AddRange(
                new DocumentTypeDefinition
                {
                    Name = "Purchase Request",
                    Code = "PurchaseRequest",
                    Description = "Standard purchase requisition",
                    Category = "financial",
                    AmountRequired = true,
                    IsActive = true,
                    CreatedAtUtc = now,
                },
                new DocumentTypeDefinition
                {
                    Name = "CapEx Request",
                    Code = "CapEx",
                    Description = "Capital expenditure approval",
                    Category = "financial",
                    AmountRequired = true,
                    IsActive = true,
                    CreatedAtUtc = now,
                });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserTypesTableAsync(WdasDbContext db)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS "UserTypes" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "Name" character varying(256) NOT NULL,
                "Code" character varying(64) NOT NULL,
                "Description" character varying(1000),
                "IsActive" boolean NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone,
                CONSTRAINT "PK_UserTypes" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserTypes_Code"
                ON "UserTypes" ("Code");
            """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch
        {
            // SQLite / alternate providers use compatible DDL without quoted identifiers.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS UserTypes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Code TEXT NOT NULL,
                    Description TEXT,
                    IsActive INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_UserTypes_Code
                    ON UserTypes (Code);
                """);
        }
    }

    private static async Task EnsureUserTypesAsync(WdasDbContext db)
    {
        if (await db.UserTypes.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.UserTypes.AddRange(
            new UserType { Name = "Permanent", Code = "Permanent", Description = "Permanent full-time employee", IsActive = true, CreatedAtUtc = now },
            new UserType { Name = "Contractor", Code = "Contractor", Description = "Contract / third-party staff", IsActive = true, CreatedAtUtc = now },
            new UserType { Name = "Intern", Code = "Intern", Description = "Intern / trainee", IsActive = true, CreatedAtUtc = now });

        await db.SaveChangesAsync();
    }
}
