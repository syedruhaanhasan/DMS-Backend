using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Domain.Services;

public class WorkflowEngineRules
{
    public static WorkflowStep GetCurrentActiveStep(Document document)
    {
        var step = document.WorkflowSteps
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault(s => s.Status is WorkflowStepStatus.Active or WorkflowStepStatus.Pending);

        if (step is null)
        {
            throw new DomainException("No active workflow step found for this document.");
        }

        if (step.Status == WorkflowStepStatus.Pending)
        {
            throw new DomainException("This workflow step is not yet visible for action.");
        }

        return step;
    }

    public static bool CanUserActOnStep(WorkflowStep step, Guid userId, IReadOnlyCollection<Guid> groupMemberUserIds)
    {
        if (step.Status != WorkflowStepStatus.Active)
        {
            return false;
        }

        if (step.ApproverUserId.HasValue)
        {
            return step.ApproverUserId.Value == userId;
        }

        if (step.ApproverGroupId.HasValue && step.GroupRequirement == GroupApprovalRequirement.AnyOneMember)
        {
            return groupMemberUserIds.Contains(userId);
        }

        return false;
    }

    public static bool ArePriorStepsComplete(Document document, WorkflowStep step)
    {
        return document.WorkflowSteps
            .Where(s => s.StepOrder < step.StepOrder)
            .All(s => s.Status is WorkflowStepStatus.Approved or WorkflowStepStatus.Skipped);
    }

    public static void ValidateSequentialVisibility(Document document, WorkflowStep step, ApprovalSequence approvalSequence = ApprovalSequence.Sequential)
    {
        if (approvalSequence == ApprovalSequence.Parallel)
        {
            if (step.Status != WorkflowStepStatus.Active)
            {
                throw new DomainException("This workflow step is not currently actionable.");
            }

            return;
        }

        if (!ArePriorStepsComplete(document, step))
        {
            throw new DomainException("Sequential visibility violation: prior steps are not complete.");
        }

        if (step.Status != WorkflowStepStatus.Active)
        {
            throw new DomainException("This workflow step is not currently actionable.");
        }
    }
}
