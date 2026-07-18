using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WDAS.Domain.Entities;

namespace WDAS.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.SequenceNumber).IsUnique();
        builder.HasIndex(e => e.DocumentId);
        builder.HasIndex(e => e.CreatedAtUtc);
        builder.Property(e => e.EntryHash).HasMaxLength(128).IsRequired();
        builder.Property(e => e.PreviousHash).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(500).IsRequired();
    }
}

public class DocumentSearchIndexConfiguration : IEntityTypeConfiguration<DocumentSearchIndex>
{
    public void Configure(EntityTypeBuilder<DocumentSearchIndex> builder)
    {
        builder.Property(i => i.Id).UseIdentityColumn();
        builder.HasIndex(i => i.DocumentId).IsUnique();
        builder.HasIndex(i => i.ArchiveDocumentId);
        builder.Property(i => i.Subject).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Amount).HasPrecision(18, 2);
        builder.HasOne(i => i.Document).WithMany().HasForeignKey(i => i.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PushDeviceRegistrationConfiguration : IEntityTypeConfiguration<PushDeviceRegistration>
{
    public void Configure(EntityTypeBuilder<PushDeviceRegistration> builder)
    {
        builder.Property(d => d.Id).UseIdentityColumn();
        builder.HasIndex(d => new { d.UserId, d.DeviceToken }).IsUnique();
        builder.Property(d => d.DeviceToken).HasMaxLength(512).IsRequired();
        builder.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId);
    }
}
