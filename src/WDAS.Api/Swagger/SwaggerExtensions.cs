using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WDAS.Api.Swagger;

public static class SwaggerExtensions
{
    public static IServiceCollection AddWdasSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "WDAS API",
                Version = "v1",
                Description = "Workflow-Based Document Approval System — Phase 1"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste the JWT from POST /api/auth/login (without the 'Bearer ' prefix).",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });

            options.OperationFilter<AllowAnonymousOperationFilter>();
        });

        return services;
    }

    public static WebApplication UseWdasSwaggerUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "WDAS API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "WDAS API";
        });

        return app;
    }

    private sealed class AllowAnonymousOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var allowAnonymous = context.MethodInfo.GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .Any();

            if (allowAnonymous)
            {
                operation.Security = [];
            }
        }
    }
}
