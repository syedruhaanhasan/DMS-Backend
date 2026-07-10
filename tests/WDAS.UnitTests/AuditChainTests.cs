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
}
