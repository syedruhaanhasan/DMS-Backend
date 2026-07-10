using System.Text.Json;
using System.Text.Json.Serialization;
using WDAS.Api.Middleware;
using WDAS.Api.Swagger;
using WDAS.Infrastructure;
using WDAS.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddWdasSwagger();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                return uri.Host is "localhost" or "127.0.0.1";
            })
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseWdasSwaggerUi();
}

app.UseCors();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<StepUpAuthMiddleware>();
app.UseMiddleware<AuditViewMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");

if (!app.Environment.IsEnvironment("Testing"))
{
    var dbProvider = WDAS.Infrastructure.Persistence.DatabaseConnection.ResolveProvider(builder.Configuration);
    app.Logger.LogInformation("Database provider: {Provider}", dbProvider);
    await DatabaseSeeder.SeedAsync(app.Services);
}

app.Run();

public partial class Program;
