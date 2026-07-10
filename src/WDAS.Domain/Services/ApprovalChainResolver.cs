using System.Text.Json;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Domain.Services;

public class ApprovalChainResolver
{
    public IReadOnlyList<ResolvedApprovalStep> Resolve(
        WorkflowVersion version,
        decimal? amount,
        IReadOnlyList<Guid>? adHocApproverUserIds)
    {
        // User-selected approvers (document "Approvers" field) always define the chain.
        if (adHocApproverUserIds is { Count: > 0 })
        {
            return ResolveAdHoc(adHocApproverUserIds);
        }

        return version.ApprovalMode switch
        {
            ApprovalMode.Matrix => ResolveMatrix(version, amount),
            ApprovalMode.Group => ResolveGroups(version),
            ApprovalMode.AdHoc => ResolveAdHoc(adHocApproverUserIds),
            ApprovalMode.Hybrid => ResolveHybrid(version, amount, adHocApproverUserIds),
            _ => throw new DomainException($"Unsupported approval mode: {version.ApprovalMode}")
        };
    }

    private static List<ResolvedApprovalStep> ResolveMatrix(WorkflowVersion version, decimal? amount)
    {
        if (!amount.HasValue)
        {
            throw new DomainException("Amount is required for matrix-based approval.");
        }

        MatrixTierValidator.Validate(version.MatrixTiers.ToList());

        var tier = version.MatrixTiers
            .OrderBy(t => t.SequenceOrder)
            .FirstOrDefault(t =>
                amount.Value >= t.MinAmount &&
                (!t.MaxAmount.HasValue || amount.Value <= t.MaxAmount.Value));

        if (tier is null)
        {
            throw new DomainException("No approval matrix tier matches the document amount.");
        }

        var approverIds = MatrixTierValidator.ParseApproverIds(tier.ApproverUserIdsJson);
        return approverIds
            .Select((id, index) => new ResolvedApprovalStep(index + 1, id, null, null, null))
            .ToList();
    }

    private static List<ResolvedApprovalStep> ResolveGroups(WorkflowVersion version)
    {
        var groups = version.ApproverGroups.OrderBy(g => g.SequenceOrder).ToList();
        if (groups.Count == 0)
        {
            throw new DomainException("At least one approver is required.");
        }

        var steps = new List<ResolvedApprovalStep>();
        var order = 1;

        foreach (var group in groups)
        {
            if (group.Members.Count == 0)
            {
                throw new DomainException($"Approver group '{group.Name}' has no members.");
            }

            if (group.Requirement == GroupApprovalRequirement.AnyOneMember)
            {
                steps.Add(new ResolvedApprovalStep(order++, null, group.Id, group.Name, group.Requirement));
            }
            else
            {
                foreach (var member in group.Members.OrderBy(m => m.User.DisplayName))
                {
                    steps.Add(new ResolvedApprovalStep(order++, member.UserId, group.Id, group.Name, group.Requirement));
                }
            }
        }

        return steps;
    }

    private static List<ResolvedApprovalStep> ResolveAdHoc(IReadOnlyList<Guid>? adHocApproverUserIds)
    {
        if (adHocApproverUserIds is null || adHocApproverUserIds.Count == 0)
        {
            throw new DomainException("Ad-hoc approvers must be selected at document creation.");
        }

        return adHocApproverUserIds
            .Select((id, index) => new ResolvedApprovalStep(index + 1, id, null, null, null))
            .ToList();
    }

    private static List<ResolvedApprovalStep> ResolveHybrid(
        WorkflowVersion version,
        decimal? amount,
        IReadOnlyList<Guid>? adHocApproverUserIds)
    {
        var steps = new List<ResolvedApprovalStep>();

        if (version.MatrixTiers.Count > 0)
        {
            steps.AddRange(ResolveMatrix(version, amount));
        }
        else if (version.ApproverGroups.Count > 0)
        {
            steps.AddRange(ResolveGroups(version));
        }

        if (adHocApproverUserIds is { Count: > 0 })
        {
            var adHocSteps = ResolveAdHoc(adHocApproverUserIds);
            var nextOrder = steps.Count + 1;
            foreach (var adHocStep in adHocSteps)
            {
                steps.Add(adHocStep with { StepOrder = nextOrder++ });
            }
        }

        if (steps.Count == 0)
        {
            throw new DomainException("Hybrid workflow requires configured matrix/group stages or selected approvers.");
        }

        return steps;
    }
}

public record ResolvedApprovalStep(
    int StepOrder,
    Guid? ApproverUserId,
    Guid? ApproverGroupId,
    string? GroupName,
    GroupApprovalRequirement? GroupRequirement);
