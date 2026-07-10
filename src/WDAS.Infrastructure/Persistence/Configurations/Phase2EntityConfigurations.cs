using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WDAS.Domain.Entities;

namespace WDAS.Infrastructure.Persistence.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.HasIndex(a => new { a.DocumentId, a.LogicalName, a.VersionNumber });
        builder.Property(a => a.FileName).HasMaxLength(500).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(1000).IsRequired();
        builder.HasOne(a => a.Document).WithMany(d => d.Attachments).HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.UploadedBy).WithMany().HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.WorkflowStepAction).WithMany().HasForeignKey(a => a.WorkflowStepActionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> builder)
    {
        builder.HasIndex(d => new { d.ApproverUserId, d.StartsAtUtc, d.EndsAtUtc });
        builder.HasOne(d => d.Approver).WithMany().HasForeignKey(d => d.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Delegate).WithMany().HasForeignKey(d => d.DelegateUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExternalApproverSessionConfiguration : IEntityTypeConfiguration<ExternalApproverSession>
{
    public void Configure(EntityTypeBuilder<ExternalApproverSession> builder)
    {
        builder.HasIndex(s => s.SecureTokenHash).IsUnique();
        builder.Property(s => s.ApproverEmail).HasMaxLength(256).IsRequired();
        builder.HasOne(s => s.WorkflowStep).WithMany().HasForeignKey(s => s.WorkflowStepId);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasIndex(n => new { n.RecipientUserId, n.Status });
        builder.Property(n => n.Subject).HasMaxLength(500).IsRequired();
        builder.HasOne(n => n.RecipientUser).WithMany().HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.Document).WithMany().HasForeignKey(n => n.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RepositoryDocumentConfiguration : IEntityTypeConfiguration<RepositoryDocument>
{
    public void Configure(EntityTypeBuilder<RepositoryDocument> builder)
    {
        builder.HasIndex(r => r.ArchiveDocumentId).IsUnique();
        builder.HasIndex(r => r.SourceDocumentId).IsUnique();
        builder.Property(r => r.ArchiveDocumentId).HasMaxLength(50).IsRequired();
        builder.HasOne(r => r.SourceDocument).WithMany().HasForeignKey(r => r.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.FinalizedBy).WithMany().HasForeignKey(r => r.FinalizedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
