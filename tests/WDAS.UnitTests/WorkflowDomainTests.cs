using System.Text.Json;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;
using WDAS.Domain.Services;

namespace WDAS.UnitTests;

public class MatrixTierValidatorTests
{
    [Fact]
    public void Validate_RejectsOverlappingTiers()
    {
        var tiers = new List<ApprovalMatrixTier>
        {
            CreateTier(1, 0, 5000, [Guid.NewGuid()]),
            CreateTier(2, 4000, 10000, [Guid.NewGuid()])
        };

        var ex = Assert.Throws<DomainException>(() => MatrixTierValidator.Validate(tiers));
        Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsGapsBetweenTiers()
    {
        var tiers = new List<ApprovalMatrixTier>
        {
            CreateTier(1, 0, 10000, [Guid.NewGuid()]),
            CreateTier(2, 20000, null, [Guid.NewGuid()])
        };

        var ex = Assert.Throws<DomainException>(() => MatrixTierValidator.Validate(tiers));
        Assert.Contains("gap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AllowsOpenEndedFinalTier()
    {
        var tiers = new List<ApprovalMatrixTier>
        {
            CreateTier(1, 0, 10000, [Guid.NewGuid()]),
            CreateTier(2, 10001, null, [Guid.NewGuid(), Guid.NewGuid()])
        };

        MatrixTierValidator.Validate(tiers);
    }

    [Fact]
    public void Validate_AllowsLegacyDecimalContinuity()
    {
        var tiers = new List<ApprovalMatrixTier>
        {
            CreateTier(1, 0, 10000, [Guid.NewGuid()]),
            CreateTier(2, 10000.01m, null, [Guid.NewGuid()])
        };

        MatrixTierValidator.Validate(tiers);
    }

    private static ApprovalMatrixTier CreateTier(int order, decimal min, decimal? max, Guid[] approvers) =>
        new()
        {
            SequenceOrder = order,
            MinAmount = min,
            MaxAmount = max,
            ApproverUserIdsJson = JsonSerializer.Serialize(approvers)
        };
}

public class ApprovalChainResolverTests
{
    private readonly ApprovalChainResolver _resolver = new();

    [Fact]
    public void Resolve_GroupMode_CreatesOrderedSteps()
    {
        var approver1 = Guid.NewGuid();
        var approver2 = Guid.NewGuid();
        var version = new WorkflowVersion
        {
            ApprovalMode = ApprovalMode.Group,
            ApproverGroups =
            [
                new ApproverGroup
                {
                    Name = "Managers",
                    SequenceOrder = 1,
                    Requirement = GroupApprovalRequirement.AnyOneMember,
                    Members = [new ApproverGroupMember { UserId = approver1 }]
                },
                new ApproverGroup
                {
                    Name = "Directors",
                    SequenceOrder = 2,
                    Requirement = GroupApprovalRequirement.AnyOneMember,
                    Members = [new ApproverGroupMember { UserId = approver2 }]
                }
            ]
        };

        var chain = _resolver.Resolve(version, null, null);

        Assert.Equal(2, chain.Count);
        Assert.Equal(1, chain[0].StepOrder);
        Assert.Equal(2, chain[1].StepOrder);
    }

    [Fact]
    public void Resolve_MatrixMode_MatchesAmountBand()
    {
        var approver = Guid.NewGuid();
        var version = new WorkflowVersion
        {
            ApprovalMode = ApprovalMode.Matrix,
            MatrixTiers =
            [
                new ApprovalMatrixTier
                {
                    SequenceOrder = 1,
                    MinAmount = 0,
                    MaxAmount = 10000,
                    ApproverUserIdsJson = JsonSerializer.Serialize(new[] { approver })
                },
                new ApprovalMatrixTier
                {
                    SequenceOrder = 2,
                    MinAmount = 10000.01m,
                    MaxAmount = null,
                    ApproverUserIdsJson = JsonSerializer.Serialize(new[] { approver })
                }
            ]
        };

        var chain = _resolver.Resolve(version, 15000m, null);

        Assert.Single(chain);
        Assert.Equal(approver, chain[0].ApproverUserId);
    }

    [Fact]
    public void Resolve_WithSelectedUsers_UsesUserChainRegardlessOfMode()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var version = new WorkflowVersion
        {
            ApprovalMode = ApprovalMode.Group,
            ApproverGroups =
            [
                new ApproverGroup
                {
                    Name = "Managers",
                    SequenceOrder = 1,
                    Requirement = GroupApprovalRequirement.AnyOneMember,
                    Members = [new ApproverGroupMember { UserId = Guid.NewGuid() }]
                }
            ]
        };

        var chain = _resolver.Resolve(version, null, [user1, user2]);

        Assert.Equal(2, chain.Count);
        Assert.Equal(user1, chain[0].ApproverUserId);
        Assert.Equal(user2, chain[1].ApproverUserId);
    }

    [Fact]
    public void Resolve_HybridMode_WithoutSelectedUsers_UsesConfiguredGroups()
    {
        var groupApprover = Guid.NewGuid();
        var version = new WorkflowVersion
        {
            ApprovalMode = ApprovalMode.Hybrid,
            ApproverGroups =
            [
                new ApproverGroup
                {
                    Name = "Stage1",
                    SequenceOrder = 1,
                    Requirement = GroupApprovalRequirement.AnyOneMember,
                    Members = [new ApproverGroupMember { UserId = groupApprover }]
                }
            ]
        };

        var chain = _resolver.Resolve(version, null, null);

        Assert.Single(chain);
        Assert.Null(chain[0].ApproverUserId);
    }
}

public class WorkflowEngineRulesTests
{
    [Fact]
    public void ValidateSequentialVisibility_BlocksWhenPriorStepIncomplete()
    {
        var document = new Document
        {
            WorkflowSteps =
            [
                new WorkflowStep { StepOrder = 1, Status = WorkflowStepStatus.Active },
                new WorkflowStep { StepOrder = 2, Status = WorkflowStepStatus.Pending }
            ]
        };

        var ex = Assert.Throws<DomainException>(() =>
            WorkflowEngineRules.ValidateSequentialVisibility(document, document.WorkflowSteps.Last()));

        Assert.Contains("Sequential visibility", ex.Message);
    }

    [Fact]
    public void CanUserActOnStep_AllowsAssignedApproverOnly()
    {
        var approverId = Guid.NewGuid();
        var step = new WorkflowStep
        {
            Status = WorkflowStepStatus.Active,
            ApproverUserId = approverId
        };

        Assert.True(WorkflowEngineRules.CanUserActOnStep(step, approverId, []));
        Assert.False(WorkflowEngineRules.CanUserActOnStep(step, Guid.NewGuid(), []));
    }
}
