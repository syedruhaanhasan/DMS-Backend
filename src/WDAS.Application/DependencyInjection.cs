using Microsoft.Extensions.DependencyInjection;
using WDAS.Application.Abstractions;
using WDAS.Application.Services;

namespace WDAS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<WorkflowService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<WorkflowEngineService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<CancellationService>();
        services.AddScoped<FinalizationService>();
        services.AddScoped<DelegationService>();
        services.AddScoped<ExternalApproverService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AuditService>();
        services.AddScoped<SearchService>();
        services.AddScoped<IDocumentSearchIndexer, DocumentSearchIndexer>();
        services.AddScoped<ReportingService>();
        services.AddScoped<MobileService>();
        services.AddScoped<UserPreferencesService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<DocumentTypeService>();
        return services;
    }
}
