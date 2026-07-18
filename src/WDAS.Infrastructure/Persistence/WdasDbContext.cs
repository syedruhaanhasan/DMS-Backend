using Microsoft.EntityFrameworkCore;
using WDAS.Application.Abstractions;
using WDAS.Domain.Entities;

namespace WDAS.Infrastructure.Persistence;

public class WdasDbContext : DbContext, IApplicationDbContext, IUnitOfWork
{
    public WdasDbContext(DbContextOptions<WdasDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RoleMapping> RoleMappings => Set<RoleMapping>();
    public DbSet<SecurityRole> SecurityRoles => Set<SecurityRole>();
    public DbSet<SecurityRolePermission> SecurityRolePermissions => Set<SecurityRolePermission>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<ApprovalMatrixTier> ApprovalMatrixTiers => Set<ApprovalMatrixTier>();
    public DbSet<ApproverGroup> ApproverGroups => Set<ApproverGroup>();
    public DbSet<ApproverGroupMember> ApproverGroupMembers => Set<ApproverGroupMember>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentRecipient> DocumentRecipients => Set<DocumentRecipient>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowStepAction> WorkflowStepActions => Set<WorkflowStepAction>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Delegation> Delegations => Set<Delegation>();
    public DbSet<ExternalApproverSession> ExternalApproverSessions => Set<ExternalApproverSession>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RepositoryDocument> RepositoryDocuments => Set<RepositoryDocument>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<DocumentSearchIndex> DocumentSearchIndexes => Set<DocumentSearchIndex>();
    public DbSet<PushDeviceRegistration> PushDeviceRegistrations => Set<PushDeviceRegistration>();
    public DbSet<DocumentTypeDefinition> DocumentTypeDefinitions => Set<DocumentTypeDefinition>();
    public DbSet<ActiveDirectorySetting> ActiveDirectorySettings => Set<ActiveDirectorySetting>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

    IQueryable<Department> IApplicationDbContext.Departments => Departments;
    IQueryable<User> IApplicationDbContext.Users => Users;
    IQueryable<RoleMapping> IApplicationDbContext.RoleMappings => RoleMappings;
    IQueryable<SecurityRole> IApplicationDbContext.SecurityRoles => SecurityRoles;
    IQueryable<SecurityRolePermission> IApplicationDbContext.SecurityRolePermissions => SecurityRolePermissions;
    IQueryable<Workflow> IApplicationDbContext.Workflows => Workflows;
    IQueryable<WorkflowVersion> IApplicationDbContext.WorkflowVersions => WorkflowVersions;
    IQueryable<ApprovalMatrixTier> IApplicationDbContext.ApprovalMatrixTiers => ApprovalMatrixTiers;
    IQueryable<ApproverGroup> IApplicationDbContext.ApproverGroups => ApproverGroups;
    IQueryable<ApproverGroupMember> IApplicationDbContext.ApproverGroupMembers => ApproverGroupMembers;
    IQueryable<Document> IApplicationDbContext.Documents => Documents;
    IQueryable<WorkflowStep> IApplicationDbContext.WorkflowSteps => WorkflowSteps;
    IQueryable<WorkflowStepAction> IApplicationDbContext.WorkflowStepActions => WorkflowStepActions;
    IQueryable<Attachment> IApplicationDbContext.Attachments => Attachments;
    IQueryable<Delegation> IApplicationDbContext.Delegations => Delegations;
    IQueryable<ExternalApproverSession> IApplicationDbContext.ExternalApproverSessions => ExternalApproverSessions;
    IQueryable<Notification> IApplicationDbContext.Notifications => Notifications;
    IQueryable<RepositoryDocument> IApplicationDbContext.RepositoryDocuments => RepositoryDocuments;
    IQueryable<AuditLogEntry> IApplicationDbContext.AuditLogEntries => AuditLogEntries;
    IQueryable<DocumentSearchIndex> IApplicationDbContext.DocumentSearchIndexes => DocumentSearchIndexes;
    IQueryable<PushDeviceRegistration> IApplicationDbContext.PushDeviceRegistrations => PushDeviceRegistrations;
    IQueryable<DocumentTypeDefinition> IApplicationDbContext.DocumentTypeDefinitions => DocumentTypeDefinitions;
    IQueryable<ActiveDirectorySetting> IApplicationDbContext.ActiveDirectorySettings => ActiveDirectorySettings;
    IQueryable<RevokedToken> IApplicationDbContext.RevokedTokens => RevokedTokens;

    void IApplicationDbContext.Add<T>(T entity) => Set<T>().Add(entity);

    void IApplicationDbContext.RemoveRange<T>(IEnumerable<T> entities) => Set<T>().RemoveRange(entities);

    Task<T?> IApplicationDbContext.FindAsync<T>(int id, CancellationToken cancellationToken) where T : class =>
        Set<T>().FindAsync([id], cancellationToken).AsTask();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WdasDbContext).Assembly);
        ApplySqlServerSafeDeleteBehaviors(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// SQL Server rejects multiple cascade delete paths. Keep cascade only on direct owned children.
    /// </summary>
    private static void ApplySqlServerSafeDeleteBehaviors(ModelBuilder modelBuilder)
    {
        var ownedChildPairs = new HashSet<(Type Dependent, Type Principal)>
        {
            (typeof(WorkflowStep), typeof(Document)),
            (typeof(Attachment), typeof(Document)),
            (typeof(DocumentRecipient), typeof(Document)),
            (typeof(DocumentSearchIndex), typeof(Document)),
            (typeof(WorkflowStepAction), typeof(WorkflowStep)),
            (typeof(WorkflowVersion), typeof(Workflow)),
            (typeof(ApprovalMatrixTier), typeof(WorkflowVersion)),
            (typeof(ApproverGroup), typeof(WorkflowVersion)),
            (typeof(ApproverGroupMember), typeof(ApproverGroup)),
        };

        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys()))
        {
            var dependent = foreignKey.DeclaringEntityType.ClrType;
            var principal = foreignKey.PrincipalEntityType.ClrType;
            var isOwnedChild = ownedChildPairs.Contains((dependent, principal));

            if (isOwnedChild)
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Cascade;
                continue;
            }

            if (foreignKey.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.SetNull)
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditLogEntry>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit log entries are append-only and cannot be modified or deleted.");
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
