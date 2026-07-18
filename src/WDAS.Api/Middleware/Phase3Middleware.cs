using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Enums;

namespace WDAS.Api.Middleware;

public class AuditViewMiddleware
{
    private static readonly HashSet<string> ViewPaths =
    [
        "/api/documents/",
        "/api/repository/",
        "/api/attachments/"
    ];

    private readonly RequestDelegate _next;

    public AuditViewMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditWriter auditWriter, ICurrentUserService currentUser)
    {
        await _next(context);

        if (context.User.Identity?.IsAuthenticated != true ||
            !HttpMethods.IsGet(context.Request.Method))
        {
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (!ViewPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (context.Response.StatusCode >= 400)
        {
            return;
        }

        int? documentId = null;
        if (path.StartsWith("/api/documents/", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(path.Split('/').ElementAtOrDefault(3), out var docId))
        {
            documentId = docId;
        }

        try
        {
            await auditWriter.WriteAsync(new AuditWriteRequest(
                AuditEventType.View,
                $"GET {path}",
                documentId,
                EntityType: "HttpRequest",
                EntityId: path,
                ActorUserId: currentUser.UserId),
                context.RequestAborted);
        }
        catch (Exception)
        {
            // Never fail the user request after the response has already started.
        }
    }
}

public class StepUpAuthMiddleware
{
    private readonly RequestDelegate _next;

    public StepUpAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method is "POST" &&
            context.Request.Path.Value?.Contains("/workflow-steps/", StringComparison.OrdinalIgnoreCase) == true &&
            (context.Request.Path.Value.Contains("/approve", StringComparison.OrdinalIgnoreCase) ||
             context.Request.Path.Value.Contains("/reject", StringComparison.OrdinalIgnoreCase)))
        {
            var requiresStepUp = context.Request.Headers.ContainsKey("X-Requires-Step-Up");
            var verified = context.Request.Headers["X-Step-Up-Verified"].FirstOrDefault() == "true";

            if (requiresStepUp && !verified)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new StepUpChallengeDto(true, "Step-up authentication required for this action."));
                return;
            }
        }

        await _next(context);
    }
}
