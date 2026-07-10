using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WDAS.Application.Abstractions;
using WDAS.Domain.Enums;

namespace WDAS.Infrastructure.Notifications;

/// <summary>
/// Periodically marks overdue workflow steps and dispatches SLA breach notifications.
/// </summary>
public class SlaEscalationHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SlaEscalationHostedService> _logger;

    public SlaEscalationHostedService(IServiceProvider services, ILogger<SlaEscalationHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOverdueStepsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SLA escalation sweep failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task ProcessOverdueStepsAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var now = clock.UtcNow;
        var overdue = await db.WorkflowSteps
            .Include(s => s.Document)
            .Include(s => s.ApproverUser)
            .Where(s => s.Status == WorkflowStepStatus.Active &&
                        s.SlaDueAtUtc != null &&
                        s.SlaDueAtUtc < now &&
                        !s.IsSlaBreached)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var step in overdue)
        {
            step.IsSlaBreached = true;
            step.UpdatedAtUtc = now;

            if (step.ApproverUserId.HasValue)
            {
                await notifications.DispatchAsync(new NotificationRequest(
                    NotificationEventType.SlaBreach,
                    step.ApproverUserId,
                    step.ApproverUser?.Email,
                    step.DocumentId,
                    step.Id,
                    $"SLA breach: {step.Document.Subject}",
                    $"Step {step.StepOrder} is overdue."),
                    cancellationToken);
            }
        }

        if (overdue.Count > 0 && db is IUnitOfWork uow)
        {
            await uow.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Marked {Count} workflow steps as SLA breached", overdue.Count);
        }
    }
}
