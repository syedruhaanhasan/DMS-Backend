using System.Security.Cryptography;
using System.Text;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Services;

public static class AuditChainVerifier
{
    public static (bool Valid, string? Message) Verify(IReadOnlyList<AuditLogEntry> entries)
    {
        if (entries.Count == 0)
        {
            return (true, null);
        }

        var ordered = entries.OrderBy(e => e.SequenceNumber).ToList();
        var expectedPrevious = "GENESIS";

        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            if (entry.PreviousHash != expectedPrevious)
            {
                return (false, $"Chain broken at sequence {entry.SequenceNumber}: previous hash mismatch.");
            }

            var computed = ComputeEntryHash(entry.PreviousHash, entry);
            if (!string.Equals(computed, entry.EntryHash, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Chain broken at sequence {entry.SequenceNumber}: entry hash mismatch.");
            }

            expectedPrevious = entry.EntryHash;
        }

        return (true, null);
    }

    public static string ComputeEntryHash(string previousHash, AuditLogEntry entry)
    {
        var payload = string.Join('|',
            previousHash,
            entry.SequenceNumber,
            entry.EventType,
            entry.ActorUserId,
            entry.DocumentId,
            entry.EntityType,
            entry.EntityId,
            entry.Action,
            entry.DetailsJson,
            entry.CreatedAtUtc.ToString("O"));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
