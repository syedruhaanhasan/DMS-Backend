using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Options;
using WDAS.Infrastructure.Audit;
using WDAS.Infrastructure.Identity;
using WDAS.Infrastructure.Notifications;
using WDAS.Infrastructure.Persistence;
using WDAS.Infrastructure.Storage;
using WDAS.Infrastructure.Time;

namespace WDAS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AttachmentOptions>(configuration.GetSection(AttachmentOptions.SectionName));
        services.Configure<ExternalApproverOptions>(configuration.GetSection(ExternalApproverOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<LdapOptions>(configuration.GetSection(LdapOptions.SectionName));
        services.Configure<SmsOptions>(configuration.GetSection(SmsOptions.SectionName));
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        var identityProvider = configuration.GetValue<string>("Identity:Provider") ?? "Dev";
        if (identityProvider.Equals("Ldap", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IIdentityProvider, LdapIdentityProvider>();
        }
        else
        {
            services.AddScoped<IIdentityProvider, DevIdentityProvider>();
        }

        var connectionString = DatabaseConnection.ResolveConnectionString(configuration);

        if (!environment.IsEnvironment("Testing"))
        {
            services.AddDbContext<WdasDbContext>(options =>
                DatabaseConnection.ConfigureDbContext(options, connectionString));
        }

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<WdasDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WdasDbContext>());
        services.AddSingleton<IArchivePdfGenerator, QuestPdfArchiveGenerator>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IAttachmentScanner>(sp =>
        {
            var attachmentOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AttachmentOptions>>().Value;
            return attachmentOptions.VirusScanEnabled
                ? sp.GetRequiredService<ClamAvAttachmentScanner>()
                : sp.GetRequiredService<DevAttachmentScanner>();
        });
        services.AddSingleton<DevAttachmentScanner>();
        services.AddSingleton<ClamAvAttachmentScanner>();
        services.AddSingleton<IAttachmentPreviewGenerator, DevAttachmentPreviewGenerator>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddHttpClient<TwilioSmsSender>();
        services.AddScoped<ISmsSender, TwilioSmsSender>();
        services.AddHttpClient("ExpoPush");
        services.AddScoped<IPushNotificationSender, ExpoPushNotificationSender>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        services.AddSingleton<IAuthorizationHandler, SuperAdminAuthorizationHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("SuperAdmin", policy => policy.RequireRole(RoleNames.SuperAdmin));
            options.AddPolicy("WorkflowAdmin", policy => policy.RequireRole(RoleNames.SuperAdmin, RoleNames.DepartmentAdmin));
            options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
        });

        services.AddApplication();
        services.AddHostedService<SlaEscalationHostedService>();
        return services;
    }
}
