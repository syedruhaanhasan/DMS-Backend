using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class ReportingService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReportingService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ApprovalTimeReportDto>> GetApprovalTimesAsync(
        int? departmentId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureReportAccess(departmentId);

        var query = _db.Documents
            .AsNoTracking()
            .Include(d => d.Workflow)
            .Include(d => d.WorkflowSteps)
            .Where(d => d.Status == DocumentStatus.Finalized);

        if (departmentId.HasValue)
        {
            query = query.Where(d => d.DepartmentId == departmentId);
        }
        else if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            query = query.Where(d => d.DepartmentId == _currentUser.DepartmentId);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(d => d.FinalizedAtUtc >= fromUtc);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(d => d.FinalizedAtUtc <= toUtc);
        }

        var documents = await query.ToListAsync(cancellationToken);
        var deptNames = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        return documents
            .GroupBy(d => new { d.DepartmentId, d.Workflow.Name })
            .Select(g =>
            {
                var endToEnd = g
                    .Where(d => d.SubmittedAtUtc.HasValue && d.FinalizedAtUtc.HasValue)
                    .Select(d => (d.FinalizedAtUtc!.Value - d.SubmittedAtUtc!.Value).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average();

                var stepHours = g.SelectMany(d => d.WorkflowSteps)
                    .Where(s => s.ActivatedAtUtc.HasValue && s.CompletedAtUtc.HasValue)
                    .Select(s => (s.CompletedAtUtc!.Value - s.ActivatedAtUtc!.Value).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average();

                return new ApprovalTimeReportDto(
                    IdParsing.ToApi(g.Key.DepartmentId),
                    deptNames.GetValueOrDefault(g.Key.DepartmentId, "Unknown"),
                    g.Key.Name,
                    endToEnd,
                    stepHours,
                    g.Count());
            })
            .OrderBy(r => r.DepartmentName)
            .ToList();
    }

    public async Task<IReadOnlyList<BottleneckReportDto>> GetBottlenecksAsync(
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        EnsureReportAccess(departmentId);

        var from = DateTime.UtcNow.AddMonths(-6);
        var steps = await _db.WorkflowSteps
            .AsNoTracking()
            .Include(s => s.ApproverUser)
            .Include(s => s.Document)
            .Where(s => s.ApproverUserId.HasValue && s.CompletedAtUtc.HasValue && s.ActivatedAtUtc.HasValue)
            .Where(s => s.CompletedAtUtc >= from)
            .Where(s => !departmentId.HasValue || s.Document.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);

        return steps
            .GroupBy(s => s.ApproverUserId!.Value)
            .Select(g => new BottleneckReportDto(
                g.First().ApproverUser?.DisplayName ?? "Unknown",
                IdParsing.ToApi(g.Key),
                g.Count(),
                g.Average(s => (s.CompletedAtUtc!.Value - s.ActivatedAtUtc!.Value).TotalHours),
                g.Count(s => s.IsSlaBreached)))
            .OrderByDescending(b => b.AverageDelayHours)
            .Take(20)
            .ToList();
    }

    public async Task<IReadOnlyList<VolumeTrendReportDto>> GetVolumeTrendsAsync(
        int months,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        EnsureReportAccess(departmentId);
        months = Math.Clamp(months, 1, 24);

        var from = DateTime.UtcNow.AddMonths(-months);
        var query = _db.Documents.AsNoTracking().Where(d => d.SubmittedAtUtc >= from);

        if (departmentId.HasValue)
        {
            query = query.Where(d => d.DepartmentId == departmentId);
        }
        else if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            query = query.Where(d => d.DepartmentId == _currentUser.DepartmentId);
        }

        var documents = await query.ToListAsync(cancellationToken);

        return documents
            .GroupBy(d => $"{d.SubmittedAtUtc!.Value:yyyy-MM}")
            .OrderBy(g => g.Key)
            .Select(g => new VolumeTrendReportDto(
                g.Key,
                g.Count(),
                g.Count(d => d.Status == DocumentStatus.Finalized),
                g.Count(d => d.Status == DocumentStatus.Rejected),
                g.Count() == 0 ? 0 : (double)g.Count(d => d.Status == DocumentStatus.Rejected) / g.Count()))
            .ToList();
    }

    public async Task<SuccessMetricsDto> GetSuccessMetricsAsync(
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        EnsureReportAccess(departmentId);
        var now = DateTime.UtcNow;
        var from30 = now.AddDays(-30);

        var usersQuery = _db.Users.AsNoTracking().Where(u => u.IsEnabledInAd && !u.IsDisabledInApp);
        var docsQuery = _db.Documents.AsNoTracking().AsQueryable();
        var stepsQuery = _db.WorkflowSteps.AsNoTracking()
            .Include(s => s.Document)
            .Where(s => s.CompletedAtUtc.HasValue && s.ActivatedAtUtc.HasValue);

        if (departmentId.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.DepartmentId == departmentId);
            docsQuery = docsQuery.Where(d => d.DepartmentId == departmentId);
            stepsQuery = stepsQuery.Where(s => s.Document.DepartmentId == departmentId);
        }
        else if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            usersQuery = usersQuery.Where(u => u.DepartmentId == _currentUser.DepartmentId);
            docsQuery = docsQuery.Where(d => d.DepartmentId == _currentUser.DepartmentId);
            stepsQuery = stepsQuery.Where(s => s.Document.DepartmentId == _currentUser.DepartmentId);
        }

        var activeUsers = await usersQuery.CountAsync(cancellationToken);
        var submittedLast30 = await docsQuery
            .CountAsync(d => d.SubmittedAtUtc >= from30, cancellationToken);

        var submittersLast30 = await docsQuery
            .Where(d => d.SubmittedAtUtc >= from30)
            .Select(d => d.OwnerUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var adoptionRate = activeUsers == 0 ? 0 : Math.Round((double)submittersLast30 / activeUsers * 100, 1);

        var finalized = await docsQuery
            .Where(d => d.Status == DocumentStatus.Finalized && d.SubmittedAtUtc.HasValue && d.FinalizedAtUtc.HasValue)
            .ToListAsync(cancellationToken);

        var avgCycleDays = finalized.Count == 0
            ? 0
            : Math.Round(finalized.Average(d => (d.FinalizedAtUtc!.Value - d.SubmittedAtUtc!.Value).TotalDays), 1);

        var completedSteps = await stepsQuery.ToListAsync(cancellationToken);
        var onTimeSteps = completedSteps.Count(s => !s.IsSlaBreached);
        var slaCompliance = completedSteps.Count == 0
            ? 100
            : Math.Round((double)onTimeSteps / completedSteps.Count * 100, 1);

        var slaBreachesQuery = _db.WorkflowSteps.AsNoTracking()
            .Include(s => s.Document)
            .Where(s => s.IsSlaBreached);

        if (departmentId.HasValue)
        {
            slaBreachesQuery = slaBreachesQuery.Where(s => s.Document.DepartmentId == departmentId);
        }
        else if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            slaBreachesQuery = slaBreachesQuery.Where(s => s.Document.DepartmentId == _currentUser.DepartmentId);
        }

        var slaBreaches = await slaBreachesQuery.CountAsync(cancellationToken);

        var auditExports = await _db.AuditLogEntries.AsNoTracking()
            .CountAsync(e => e.EventType == Domain.Enums.AuditEventType.Export &&
                             e.CreatedAtUtc >= from30, cancellationToken);

        return new SuccessMetricsDto(
            avgCycleDays,
            adoptionRate,
            slaBreaches,
            auditExports,
            slaCompliance,
            activeUsers,
            submittedLast30);
    }

    private void EnsureReportAccess(int? departmentId)
    {
        if (_currentUser.IsInRole(RoleNames.SuperAdmin) || _currentUser.IsInRole(RoleNames.Auditor))
        {
            return;
        }

        if (_currentUser.IsInRole(RoleNames.DepartmentAdmin))
        {
            if (departmentId.HasValue && departmentId != _currentUser.DepartmentId)
            {
                throw new DomainException("Department admins can only view their own department reports.");
            }

            return;
        }

        throw new DomainException("Reporting requires admin or auditor access.");
    }
}

public class MobileService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public MobileService(IApplicationDbContext db, ICurrentUserService currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task RegisterDeviceAsync(RegisterPushDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _db.PushDeviceRegistrations
            .FirstOrDefaultAsync(d => d.UserId == _currentUser.UserId && d.DeviceToken == request.DeviceToken, cancellationToken);

        if (existing is not null)
        {
            existing.IsActive = true;
            existing.LastUsedAtUtc = _clock.UtcNow;
            existing.Platform = request.Platform;
        }
        else
        {
            _db.Add(new Domain.Entities.PushDeviceRegistration
            {
                UserId = _currentUser.UserId,
                Platform = request.Platform,
                DeviceToken = request.DeviceToken,
                RegisteredAtUtc = _clock.UtcNow,
                LastUsedAtUtc = _clock.UtcNow
            });
        }

        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<StepUpChallengeDto> GetStepUpRequirementAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .Include(d => d.Workflow)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");

        var version = await _db.WorkflowVersions
            .Where(v => v.Id == document.WorkflowVersionId)
            .Select(v => new { v.SlaThresholdHours, v.ApprovalMode })
            .FirstOrDefaultAsync(cancellationToken);

        var requiresStepUp = document.Amount >= 10000m ||
                             document.Priority == DocumentPriority.Critical ||
                             version?.ApprovalMode == ApprovalMode.Matrix;

        return new StepUpChallengeDto(requiresStepUp, requiresStepUp ? "High-value or financial workflow requires step-up authentication." : null);
    }
}
