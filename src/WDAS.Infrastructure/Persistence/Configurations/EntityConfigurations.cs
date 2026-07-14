using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;

namespace WDAS.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.AdObjectId).IsUnique();
        builder.HasIndex(u => u.UserPrincipalName).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512);
        builder.HasOne(u => u.Department).WithMany(d => d.Users).HasForeignKey(u => u.DepartmentId);
        builder.HasOne(u => u.Manager).WithMany(u => u.DirectReports).HasForeignKey(u => u.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.HasIndex(d => d.AdObjectId).IsUnique();
        builder.HasIndex(d => d.Code).IsUnique();
        builder.Property(d => d.Name).HasMaxLength(256).IsRequired();
        builder.HasOne(d => d.ParentDepartment).WithMany(d => d.ChildDepartments).HasForeignKey(d => d.ParentDepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DocumentTypeDefinitionConfiguration : IEntityTypeConfiguration<DocumentTypeDefinition>
{
    public void Configure(EntityTypeBuilder<DocumentTypeDefinition> builder)
    {
        builder.HasIndex(t => t.Code).IsUnique();
        builder.Property(t => t.Name).HasMaxLength(256).IsRequired();
        builder.Property(t => t.Code).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.Category).HasMaxLength(32).IsRequired();
        builder.Property(t => t.AmountRequired).HasDefaultValue(false);
    }
}

public class RoleMappingConfiguration : IEntityTypeConfiguration<RoleMapping>
{
    public void Configure(EntityTypeBuilder<RoleMapping> builder)
    {
        builder.HasIndex(r => new { r.UserId, r.RoleId, r.DepartmentId }).IsUnique();
        builder.HasOne(r => r.User).WithMany(u => u.RoleMappings).HasForeignKey(r => r.UserId);
        builder.HasOne(r => r.Role).WithMany(role => role.RoleMappings).HasForeignKey(r => r.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SecurityRoleConfiguration : IEntityTypeConfiguration<SecurityRole>
{
    public void Configure(EntityTypeBuilder<SecurityRole> builder)
    {
        builder.ToTable("SecurityRoles");
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasMaxLength(120).IsRequired();
        builder.Property(r => r.Code).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(1000);
    }
}

public class SecurityRolePermissionConfiguration : IEntityTypeConfiguration<SecurityRolePermission>
{
    public void Configure(EntityTypeBuilder<SecurityRolePermission> builder)
    {
        builder.ToTable("SecurityRolePermissions");
        builder.HasIndex(p => new { p.RoleId, p.PermissionKey }).IsUnique();
        builder.Property(p => p.PermissionKey).HasMaxLength(120).IsRequired();
        builder.HasOne(p => p.Role).WithMany(r => r.Permissions).HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.HasIndex(w => new { w.DepartmentId, w.DocumentType, w.Name }).IsUnique();
        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(w => w.Department).WithMany(d => d.Workflows).HasForeignKey(w => w.DepartmentId);
    }
}

public class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
    {
        builder.HasIndex(v => new { v.WorkflowId, v.VersionNumber }).IsUnique();
        builder.HasOne(v => v.Workflow).WithMany(w => w.Versions).HasForeignKey(v => v.WorkflowId);
    }
}

public class ApprovalMatrixTierConfiguration : IEntityTypeConfiguration<ApprovalMatrixTier>
{
    public void Configure(EntityTypeBuilder<ApprovalMatrixTier> builder)
    {
        builder.HasIndex(t => new { t.WorkflowVersionId, t.SequenceOrder }).IsUnique();
        builder.Property(t => t.MinAmount).HasPrecision(18, 2);
        builder.Property(t => t.MaxAmount).HasPrecision(18, 2);
    }
}

public class ApproverGroupConfiguration : IEntityTypeConfiguration<ApproverGroup>
{
    public void Configure(EntityTypeBuilder<ApproverGroup> builder)
    {
        builder.HasIndex(g => new { g.WorkflowVersionId, g.SequenceOrder }).IsUnique();
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
    }
}

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.Property(d => d.RecordNumber)
            .ValueGeneratedOnAdd()
            .UseIdentityColumn();

        builder.HasIndex(d => d.RecordNumber).IsUnique();
        builder.HasIndex(d => d.SubmitIdempotencyKey);
        builder.Property(d => d.Subject).HasMaxLength(500).IsRequired();
        builder.Property(d => d.Amount).HasPrecision(18, 2);
        builder.HasOne(d => d.Owner).WithMany(u => u.OwnedDocuments).HasForeignKey(d => d.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Department).WithMany().HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Workflow).WithMany().HasForeignKey(d => d.WorkflowId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.WorkflowVersion).WithMany(v => v.Documents).HasForeignKey(d => d.WorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.HasIndex(s => new { s.DocumentId, s.StepOrder }).IsUnique();
        builder.HasOne(s => s.Document).WithMany(d => d.WorkflowSteps).HasForeignKey(s => s.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.WorkflowVersion).WithMany().HasForeignKey(s => s.WorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.ApproverUser).WithMany().HasForeignKey(s => s.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowStepActionConfiguration : IEntityTypeConfiguration<WorkflowStepAction>
{
    public void Configure(EntityTypeBuilder<WorkflowStepAction> builder)
    {
        builder.HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
