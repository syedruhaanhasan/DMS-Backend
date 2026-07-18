using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class DelegationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly INotificationDispatcher _notifications;
    private readonly DocumentService _documentService;

    public DelegationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        INotificationDispatcher notifications,
        DocumentService documentService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
        _documentService = documentService;
    }

    public async Task<DelegationDto> CreateDelegationAsync(CreateDelegationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EndsAtUtc <= request.StartsAtUtc)
        {
            throw new DomainException("Delegation end date must be after start date.");
        }

        var approver = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, cancellationToken)
            ?? throw new DomainException("Current user not found.");

        var delegateUserId = IdParsing.ParseRequired(request.DelegateUserId, "Delegate user id");
        var delegateUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == delegateUserId, cancellationToken)
            ?? throw new DomainException("Delegate user not found.");

        if (delegateUser.Id == approver.Id)
        {
            throw new DomainException("Cannot delegate to yourself.");
        }

        var now = _clock.UtcNow;
        var delegation = new Delegation
        {
            ApproverUserId = approver.Id,
            DelegateUserId = delegateUser.Id,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            IsActive = request.IsActive,
            Reason = request.Reason,
            AutoReplyMessage = request.AutoReplyMessage,
            CreatedAtUtc = now
        };

        _db.Add(delegation);
        await SaveAsync(cancellationToken);

        await _notifications.DispatchAsync(new NotificationRequest(
            Domain.Enums.NotificationEventType.DelegationNotice,
            delegateUser.Id,
            delegateUser.Email,
            null,
            null,
            "Approval delegation assigned",
            $"{approver.DisplayName} delegated approvals to you until {request.EndsAtUtc:u}."),
            cancellationToken);

        return new DelegationDto(
            IdParsing.ToApi(delegation.Id),
            IdParsing.ToApi(approver.Id),
            approver.DisplayName,
            IdParsing.ToApi(delegateUser.Id),
            delegateUser.DisplayName,
            delegation.StartsAtUtc,
            delegation.EndsAtUtc,
            delegation.IsActive,
            delegation.Reason);
    }

    public async Task<IReadOnlyList<DelegationDto>> ListDelegationsAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Delegations
            .Include(d => d.Approver)
            .Include(d => d.Delegate)
            .AsQueryable();

        if (isActive == true)
        {
            query = query.Where(d => d.IsActive);
        }
        else if (isActive == false)
        {
            query = query.Where(d => !d.IsActive);
        }

        if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            var userId = _currentUser.UserId;
            if (userId == 0)
            {
                throw new DomainException("Authentication required.");
            }
            query = query.Where(d => d.ApproverUserId == userId || d.DelegateUserId == userId);
        }

        var rows = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(d => new DelegationDto(
            IdParsing.ToApi(d.Id),
            IdParsing.ToApi(d.ApproverUserId),
            d.Approver.DisplayName,
            IdParsing.ToApi(d.DelegateUserId),
            d.Delegate.DisplayName,
            d.StartsAtUtc,
            d.EndsAtUtc,
            d.IsActive,
            d.Reason)).ToList();
    }

    public async Task<DelegationDto> SetDelegationActiveStatusAsync(int delegationId, bool isActive, CancellationToken cancellationToken = default)
    {
        var delegation = await _db.Delegations
            .Include(d => d.Approver)
            .Include(d => d.Delegate)
            .FirstOrDefaultAsync(d => d.Id == delegationId, cancellationToken)
            ?? throw new DomainException("Delegation not found.");

        if (delegation.ApproverUserId != _currentUser.UserId && !_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            throw new DomainException("You are not authorized to update this delegation.");
        }

        delegation.IsActive = isActive;
        delegation.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        return new DelegationDto(
            IdParsing.ToApi(delegation.Id),
            IdParsing.ToApi(delegation.ApproverUserId),
            delegation.Approver.DisplayName,
            IdParsing.ToApi(delegation.DelegateUserId),
            delegation.Delegate.DisplayName,
            delegation.StartsAtUtc,
            delegation.EndsAtUtc,
            delegation.IsActive,
            delegation.Reason);
    }

    public async Task DeactivateDelegationAsync(int delegationId, CancellationToken cancellationToken = default)
    {
        var delegation = await _db.Delegations.FirstOrDefaultAsync(d => d.Id == delegationId, cancellationToken)
            ?? throw new DomainException("Delegation not found.");

        if (delegation.ApproverUserId != _currentUser.UserId && !_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            throw new DomainException("You are not authorized to deactivate this delegation.");
        }

        delegation.IsActive = false;
        delegation.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);
    }

    public async Task<DocumentDto> ReassignStepAsync(int stepId, ReassignStepRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new DomainException("A reason is required for reassignment.");
        }

        if (!_currentUser.IsInRole(RoleNames.SuperAdmin) && !_currentUser.IsInRole(RoleNames.DepartmentAdmin))
        {
            throw new DomainException("Only department or super admins can reassign workflow steps.");
        }

        var step = await _db.WorkflowSteps
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken)
            ?? throw new DomainException("Workflow step not found.");

        if (step.Status != Domain.Enums.WorkflowStepStatus.Active)
        {
            throw new DomainException("Only the active step can be reassigned.");
        }

        var newApprover = await _db.Users.FirstOrDefaultAsync(u => u.Id == IdParsing.ParseRequired(request.NewApproverUserId, "New approver user id"), cancellationToken)
            ?? throw new DomainException("New approver not found.");

        var now = _clock.UtcNow;
        var previousApproverId = step.ApproverUserId;
        step.ApproverUserId = newApprover.Id;
        step.UpdatedAtUtc = now;

        _db.Add(new WorkflowStepAction
        {
            WorkflowStepId = step.Id,
            ActorUserId = _currentUser.UserId,
            ActionType = Domain.Enums.WorkflowActionType.Reassign,
            Comment = $"Reassigned from {previousApproverId} to {newApprover.Id}: {request.Reason}",
            ActionAtUtc = now,
            CreatedAtUtc = now
        });

        await SaveAsync(cancellationToken);
        return await _documentService.GetDocumentAsync(step.DocumentId, cancellationToken);
    }

    internal async Task<int?> ResolveDelegateApproverIdAsync(int approverUserId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        return await _db.Delegations
            .Where(d => d.ApproverUserId == approverUserId && d.IsActive && d.StartsAtUtc <= now && d.EndsAtUtc >= now)
            .Select(d => (int?)d.DelegateUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
