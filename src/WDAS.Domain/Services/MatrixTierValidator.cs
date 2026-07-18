using System.Text.Json;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Domain.Services;

public static class MatrixTierValidator
{
    public static void Validate(IReadOnlyList<ApprovalMatrixTier> tiers)
    {
        if (tiers.Count == 0)
        {
            throw new DomainException("At least one approval matrix tier is required.");
        }

        var ordered = tiers.OrderBy(t => t.MinAmount).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var tier = ordered[i];
            if (tier.MinAmount < 0)
            {
                throw new DomainException("Matrix tier minimum amount cannot be negative.");
            }

            if (tier.MaxAmount.HasValue && tier.MaxAmount.Value < tier.MinAmount)
            {
                throw new DomainException($"Matrix tier {i + 1} has max less than min.");
            }

            if (ParseApproverIds(tier.ApproverUserIdsJson).Count == 0)
            {
                throw new DomainException($"Matrix tier {i + 1} must have at least one approver.");
            }

            if (i > 0)
            {
                var previous = ordered[i - 1];
                if (!previous.MaxAmount.HasValue)
                {
                    throw new DomainException("Only the last matrix tier may have an open-ended maximum.");
                }

                if (tier.MinAmount <= previous.MaxAmount.Value)
                {
                    throw new DomainException("Matrix tiers cannot overlap.");
                }

                // Amounts are whole PKR in the UI (e.g. 0–100000 then 100001+).
                // Also accept +0.01 for older seed/decimal configs.
                var expectedWhole = previous.MaxAmount.Value + 1m;
                var expectedDecimal = previous.MaxAmount.Value + 0.01m;
                if (tier.MinAmount != expectedWhole && tier.MinAmount != expectedDecimal)
                {
                    throw new DomainException(
                        $"Matrix tiers cannot have gaps. Band {i + 1} should start at {expectedWhole} (immediately after {previous.MaxAmount.Value}).");
                }
            }
        }
    }

    public static List<int> ParseApproverIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<int>>(json) ?? [];
    }
}
