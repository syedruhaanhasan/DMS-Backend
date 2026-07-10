using WDAS.Domain.Entities;

namespace WDAS.Application.Abstractions;

public interface IApplicationDbContext
{
    IQueryable<Department> Departments { get; }
    IQueryable<User> Users { get; }
    IQueryable<RoleMapping> RoleMappings { get; }
    IQueryable<Workflow> Workflows { get; }
    IQueryable<WorkflowVersion> WorkflowVersions { get; }
    IQueryable<ApprovalMatrixTier> ApprovalMatrixTiers { get; }
    IQueryable<ApproverGroup> ApproverGroups { get; }
    IQueryable<ApproverGroupMember> ApproverGroupMembers { get; }
    IQueryable<Document> Documents { get; }
    IQueryable<WorkflowStep> WorkflowSteps { get; }
    IQueryable<WorkflowStepAction> WorkflowStepActions { get; }
    IQueryable<Attachment> Attachments { get; }
    IQueryable<Delegation> Delegations { get; }
    IQueryable<ExternalApproverSession> ExternalApproverSessions { get; }
    IQueryable<Notification> Notifications { get; }
    IQueryable<RepositoryDocument> RepositoryDocuments { get; }
    IQueryable<AuditLogEntry> AuditLogEntries { get; }
    IQueryable<DocumentSearchIndex> DocumentSearchIndexes { get; }
    IQueryable<PushDeviceRegistration> PushDeviceRegistrations { get; }
    IQueryable<DocumentTypeDefinition> DocumentTypeDefinitions { get; }

    void Add<T>(T entity) where T : class;
    void RemoveRange<T>(IEnumerable<T> entities) where T : class;
    Task<T?> FindAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class;
}
