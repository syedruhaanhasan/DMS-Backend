using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

        try
        {
            await db.Database.MigrateAsync();
        }
        catch
        {
            await db.Database.EnsureCreatedAsync();
        }

        await EnsureWorkflowVersionColumnsAsync(db);
        await EnsureDocumentTypesTableAsync(db);
        await EnsureDocumentTypeColumnsAsync(db);
        await EnsureActiveWorkflowVersionsAsync(db);
        await EnsureDocumentTypesAsync(db);

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
                Id = devUser.UserId,
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
            db.RoleMappings.Add(new RoleMapping
            {
                UserId = user.Id,
                Role = devUser.Role,
                DepartmentId = devUser.Role == ApplicationRole.SuperAdmin ? null : devUser.DepartmentId,
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureDevUserRoleAsync(
        WdasDbContext db,
        Guid userId,
        ApplicationRole role,
        Guid departmentId,
        DateTime now)
    {
        var hasRole = await db.RoleMappings.AnyAsync(r => r.UserId == userId && r.Role == role);
        if (hasRole)
        {
            return;
        }

        db.RoleMappings.Add(new RoleMapping
        {
            UserId = userId,
            Role = role,
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
                Id = devUser.UserId,
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
            db.RoleMappings.Add(new RoleMapping
            {
                UserId = user.Id,
                Role = devUser.Role,
                DepartmentId = devUser.Role == ApplicationRole.SuperAdmin ? null : devUser.DepartmentId,
                CreatedAtUtc = now
            });
        }

        var financeDept = departmentMap["ad-fin"];
        var approver1 = DevIdentityProvider.GetSeedUsers().First(u => u.UserPrincipalName == "approver.one");
        var approver2 = DevIdentityProvider.GetSeedUsers().First(u => u.UserPrincipalName == "approver.two");

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
                new ApproverGroupMember { UserId = approver1.UserId, CreatedAtUtc = now }
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
                new ApproverGroupMember { UserId = approver2.UserId, CreatedAtUtc = now }
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
                    ApproverUserIdsJson = JsonSerializer.Serialize(new[] { approver1.UserId }),
                    CreatedAtUtc = now
                },
                new ApprovalMatrixTier
                {
                    SequenceOrder = 2,
                    MinAmount = 10000.01m,
                    MaxAmount = null,
                    ApproverUserIdsJson = JsonSerializer.Serialize(new[] { approver1.UserId, approver2.UserId }),
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
                "Id" uuid NOT NULL,
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
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260709121000_AddDocumentTypeDefinitions', '10.0.0'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = '20260709121000_AddDocumentTypeDefinitions'
                );
                """);
        }
        catch
        {
            // SQLite / alternate providers use compatible DDL without quoted identifiers.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS DocumentTypeDefinitions (
                    Id TEXT NOT NULL PRIMARY KEY,
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
}
