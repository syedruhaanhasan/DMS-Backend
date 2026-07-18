using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Services;

namespace WDAS.UnitTests;

public class AuditChainTests
{
    [Fact]
    public void Verify_ValidChain_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var first = new AuditLogEntry
        {
            SequenceNumber = 1,
            PreviousHash = "GENESIS",
            EventType = AuditEventType.Create,
            Action = "Created document",
            CreatedAtUtc = now
        };
        first.EntryHash = AuditChainVerifier.ComputeEntryHash("GENESIS", first);

        var second = new AuditLogEntry
        {
            SequenceNumber = 2,
            PreviousHash = first.EntryHash,
            EventType = AuditEventType.Approve,
            Action = "Approved step",
            CreatedAtUtc = now.AddMinutes(1)
        };
        second.EntryHash = AuditChainVerifier.ComputeEntryHash(first.EntryHash, second);

        var (valid, message) = AuditChainVerifier.Verify([first, second]);

        Assert.True(valid);
        Assert.Null(message);
    }

    [Fact]
    public void Verify_TamperedEntry_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var entry = new AuditLogEntry
        {
            SequenceNumber = 1,
            PreviousHash = "GENESIS",
            EventType = AuditEventType.View,
            Action = "Viewed document",
            CreatedAtUtc = now,
            EntryHash = "TAMPERED"
        };

        var (valid, message) = AuditChainVerifier.Verify([entry]);

        Assert.False(valid);
        Assert.Contains("hash mismatch", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_LegacyHashVersion_UsesLegacyGuidFields()
    {
        var now = DateTime.UtcNow;
        var legacyActor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var legacyDocument = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var entry = new AuditLogEntry
        {
            SequenceNumber = 1,
            PreviousHash = "GENESIS",
            EventType = AuditEventType.Approve,
            Action = "Approved step",
            CreatedAtUtc = now,
            HashVersion = 1,
            LegacyActorUserId = legacyActor,
            LegacyDocumentId = legacyDocument,
            ActorUserId = 99,
            DocumentId = 100,
            EntityType = "Document",
            EntityId = legacyDocument.ToString(),
        };
        entry.EntryHash = AuditChainVerifier.ComputeEntryHash("GENESIS", entry);

        var (valid, message) = AuditChainVerifier.Verify([entry]);

        Assert.True(valid);
        Assert.Null(message);
    }

    [Fact]
    public void Verify_NewHashVersion_UsesIntegerFields()
    {
        var now = DateTime.UtcNow;
        var entry = new AuditLogEntry
        {
            SequenceNumber = 1,
            PreviousHash = "GENESIS",
            EventType = AuditEventType.Create,
            Action = "Created document",
            CreatedAtUtc = now,
            HashVersion = 2,
            ActorUserId = 5,
            DocumentId = 8,
            EntityType = "Document",
            EntityId = "8",
        };
        entry.EntryHash = AuditChainVerifier.ComputeEntryHash("GENESIS", entry);

        var (valid, message) = AuditChainVerifier.Verify([entry]);

        Assert.True(valid);
        Assert.Null(message);
    }
}
