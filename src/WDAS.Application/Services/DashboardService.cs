using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Enums;

namespace WDAS.Application.Services;

public class DashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DashboardService(IApplicationDbContext db, ICurrentUserService currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PersonalDashboardDto> GetPersonalDashboardAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var now = _clock.UtcNow;

        var delegatedApproverIds = await _db.Delegations
            .Where(d => d.DelegateUserId == userId && d.IsActive && d.StartsAtUtc <= now && d.EndsAtUtc >= now)
            .Select(d => d.ApproverUserId)
            .ToListAsync(cancellationToken);

        var pendingSteps = await _db.WorkflowSteps
            .AsNoTracking()
            .Include(s => s.Document)
                .ThenInclude(d => d.Workflow)
            .Where(s => s.Status == WorkflowStepStatus.Active &&
                        (s.ApproverUserId == userId || (s.ApproverUserId.HasValue && delegatedApproverIds.Contains(s.ApproverUserId.Value))))
            .ToListAsync(cancellationToken);

        var directPending = pendingSteps
            .Where(s => s.ApproverUserId == userId)
            .Select(s => MapItem(s.Document, s, false))
            .ToList();

        var delegatedPending = pendingSteps
            .Where(s => s.ApproverUserId.HasValue && s.ApproverUserId != userId && delegatedApproverIds.Contains(s.ApproverUserId.Value))
            .Select(s => MapItem(s.Document, s, true))
            .ToList();

        var myDocuments = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Workflow)
            .Include(d => d.WorkflowSteps)
            .Where(d => d.OwnerUserId == userId && d.Status != DocumentStatus.Finalized)
            .OrderByDescending(d => d.UpdatedAtUtc ?? d.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var recentlyCompleted = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Workflow)
            .Include(d => d.WorkflowSteps)
            .Where(d => d.OwnerUserId == userId &&
                        (d.Status == DocumentStatus.Finalized || d.Status == DocumentStatus.Rejected || d.Status == DocumentStatus.Cancelled))
            .OrderByDescending(d => d.UpdatedAtUtc ?? d.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        return new PersonalDashboardDto(
            directPending,
            delegatedPending,
            myDocuments.Select(d => MapItem(d, d.WorkflowSteps.FirstOrDefault(st => st.Status == WorkflowStepStatus.Active), false)).ToList(),
            recentlyCompleted.Select(d => MapItem(d, null, false)).ToList());
    }

    public async Task<IReadOnlyList<DashboardDocumentItemDto>> GetDocumentsForReviewAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var documents = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Workflow)
            .Include(d => d.WorkflowSteps)
            .Where(d => d.Status != DocumentStatus.Draft && d.Recipients.Any(r => r.ReviewerUserId == userId))
            .OrderByDescending(d => d.UpdatedAtUtc ?? d.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return documents
            .Select(d => MapItem(d, d.WorkflowSteps.FirstOrDefault(s => s.Status == WorkflowStepStatus.Active), false))
            .ToList();
    }

    public async Task<DepartmentDashboardDto> GetDepartmentDashboardAsync(int departmentId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(RoleNames.SuperAdmin) &&
            !_currentUser.IsInRole(RoleNames.DepartmentAdmin))
        {
            throw new Domain.Exceptions.DomainException("Department dashboard requires admin access.");
        }

        if (_currentUser.IsInRole(RoleNames.DepartmentAdmin) && _currentUser.DepartmentId != departmentId)
        {
            throw new Domain.Exceptions.DomainException("Department admins can only view their own department.");
        }

        var department = await _db.Departments.FirstAsync(d => d.Id == departmentId, cancellationToken);

        var documents = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Workflow)
            .Include(d => d.WorkflowSteps)
            .Where(d => d.DepartmentId == departmentId)
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return new DepartmentDashboardDto(
            IdParsing.ToApi(department.Id),
            department.Name,
            documents.Select(d => MapItem(d, d.WorkflowSteps.FirstOrDefault(s => s.Status == WorkflowStepStatus.Active), false)).ToList());
    }

    private DashboardDocumentItemDto MapItem(Domain.Entities.Document document, Domain.Entities.WorkflowStep? activeStep, bool isDelegated)
    {
        var classification = "OnTime";
        if (activeStep?.IsSlaBreached == true)
        {
            classification = "Overdue";
        }
        else if (activeStep?.SlaDueAtUtc is { } due && due <= _clock.UtcNow.AddHours(4))
        {
            classification = "AtRisk";
        }

        return new DashboardDocumentItemDto(
            IdParsing.ToApi(document.Id),
            document.RecordNumber,
            IdParsing.ToApi(document.OwnerUserId),
            document.Subject,
            document.Status.ToString(),
            document.Workflow.Name,
            document.SubmittedAtUtc,
            activeStep?.SlaDueAtUtc,
            activeStep?.IsSlaBreached ?? false,
            classification,
            activeStep is null ? null : IdParsing.ToApi(activeStep.Id),
            isDelegated,
            activeStep?.SeenByApproverAtUtc is not null);
    }
}
