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

                if (tier.MinAmount != previous.MaxAmount.Value + 0.01m)
                {
                    throw new DomainException("Matrix tiers cannot have gaps.");
                }
            }
        }
    }

    public static List<Guid> ParseApproverIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
    }
}
