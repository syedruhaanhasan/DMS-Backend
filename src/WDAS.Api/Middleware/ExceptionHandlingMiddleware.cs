using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using WDAS.Domain.Exceptions;

namespace WDAS.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning(ex, "Conflict detected.");
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = ex.Code,
                error = ex.Message,
                details = ex.Details
            }));
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain validation failed.");
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _logger.LogWarning(ex, "Unique constraint violation.");
            var message = ex.InnerException.Message.Contains("IX_Workflows_DepartmentId_DocumentType_Name", StringComparison.Ordinal)
                ? "A workflow with this name, document type, and department already exists."
                : "A record with the same unique values already exists.";
            await WriteErrorAsync(context, HttpStatusCode.Conflict, message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict.");
            await WriteErrorAsync(context, HttpStatusCode.Conflict, "The record changed while saving. Refresh and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception.");
            var message = _environment.IsEnvironment("Testing")
                ? ex.ToString()
                : "An unexpected error occurred.";
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, message);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
