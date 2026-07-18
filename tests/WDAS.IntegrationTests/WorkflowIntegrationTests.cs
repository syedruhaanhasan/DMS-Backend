using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Enums;
using WDAS.Infrastructure.Persistence;
using WDAS.Infrastructure.Seeding;

namespace WDAS.IntegrationTests;

public class WdasWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    public static string TestDatabasePath { get; } = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WdasDbContext>>();
            services.RemoveAll<WdasDbContext>();
            services.RemoveAll<IApplicationDbContext>();
            services.RemoveAll<IUnitOfWork>();

            _connection = new SqliteConnection($"Data Source=wdas-tests-{TestDatabasePath};Mode=Memory;Cache=Shared");
            _connection.Open();
            services.AddDbContext<WdasDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<WdasDbContext>());
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WdasDbContext>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }

        base.Dispose(disposing);
    }
}

public class WorkflowIntegrationTests : IClassFixture<WdasWebApplicationFactory>
{
    private readonly WdasWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public WorkflowIntegrationTests(WdasWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EndToEnd_DocumentCanFlowThroughMultiStepApproval()
    {
        await ResetDatabaseAsync();

        var (ownerToken, _) = await LoginAsync("maker.owner", "Owner123!");
        var (approver1Token, approver1Id) = await LoginAsync("approver.one", "Approver123!");
        var (approver2Token, approver2Id) = await LoginAsync("approver.two", "Approver123!");
        var adHocApprovers = new[] { approver1Id, approver2Id };

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var workflows = await _client.GetFromJsonAsync<List<WorkflowDto>>("/api/workflows", _jsonOptions);
        Assert.NotNull(workflows);
        var workflow = workflows!.First(w => w.Name == "Purchase Request");

        var createRequest = new CreateDocumentRequest(
            workflow.Id,
            "Finance Leadership",
            "Laptop purchase request",
            "<p>Requesting approval for 5 laptops.</p>",
            null,
            DocumentPriority.Normal,
            [new DocumentRecipientInput("Finance Leadership", "finance.leadership@wdas.local")],
            adHocApprovers,
            true,
            "submit-key-1");

        var createResponse = await _client.PostAsJsonAsync("/api/documents", createRequest);
        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Create document failed: {error}");
        }
        var document = await createResponse.Content.ReadFromJsonAsync<DocumentDto>(_jsonOptions);
        Assert.NotNull(document);
        Assert.Equal(DocumentStatus.InApproval, document!.Status);
        Assert.True(document.IsBodyLocked);
        Assert.Equal(2, document.WorkflowSteps.Count);

        var step1 = document.WorkflowSteps.First(s => s.StepOrder == 1);
        var step2 = document.WorkflowSteps.First(s => s.StepOrder == 2);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", approver2Token);
        var prematureApprove = await _client.PostAsJsonAsync($"/api/workflow-steps/{step2.Id}/approve", new WorkflowActionRequest("Too early"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, prematureApprove.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", approver1Token);
        var approve1 = await _client.PostAsJsonAsync($"/api/workflow-steps/{step1.Id}/approve", new WorkflowActionRequest("Looks good"));
        if (!approve1.IsSuccessStatusCode)
        {
            Assert.Fail($"Approve step 1 failed: {await approve1.Content.ReadAsStringAsync()}");
        }
        var afterStep1 = await approve1.Content.ReadFromJsonAsync<DocumentDto>(_jsonOptions);
        Assert.Equal(WorkflowStepStatus.Approved, afterStep1!.WorkflowSteps.First(s => s.Id == step1.Id).Status);
        Assert.Equal(WorkflowStepStatus.Active, afterStep1.WorkflowSteps.First(s => s.Id == step2.Id).Status);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", approver2Token);
        var approve2 = await _client.PostAsJsonAsync($"/api/workflow-steps/{step2.Id}/approve", new WorkflowActionRequest("Approved"));
        approve2.EnsureSuccessStatusCode();
        var completed = await approve2.Content.ReadFromJsonAsync<DocumentDto>(_jsonOptions);
        Assert.Equal(DocumentStatus.ReadyForFinalization, completed!.Status);
    }

    [Fact]
    public async Task ReturnForCorrection_RestartsApprovalFromFirstStep()
    {
        await ResetDatabaseAsync();

        var (ownerToken, _) = await LoginAsync("maker.owner", "Owner123!");
        var (approver1Token, approver1Id) = await LoginAsync("approver.one", "Approver123!");
        var (_, approver2Id) = await LoginAsync("approver.two", "Approver123!");
        var adHocApprovers = new[] { approver1Id, approver2Id };

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var workflow = (await _client.GetFromJsonAsync<List<WorkflowDto>>("/api/workflows", _jsonOptions))!
            .First(w => w.Name == "Purchase Request");

        var createResponse = await _client.PostAsJsonAsync("/api/documents", new CreateDocumentRequest(
            workflow.Id,
            "Finance Leadership",
            "Return path test",
            "<p>Initial body</p>",
            null,
            DocumentPriority.Normal,
            [],
            adHocApprovers,
            true,
            "return-key-1"));
        createResponse.EnsureSuccessStatusCode();
        var document = await createResponse.Content.ReadFromJsonAsync<DocumentDto>(_jsonOptions);

        var step1 = document!.WorkflowSteps.First();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", approver1Token);
        var returned = await _client.PostAsJsonAsync($"/api/workflow-steps/{step1.Id}/return", new WorkflowActionRequest("Please fix amount"));
        returned.EnsureSuccessStatusCode();
        var returnedDoc = await returned.Content.ReadFromJsonAsync<DocumentDto>(_jsonOptions);
        Assert.Equal(DocumentStatus.ReturnedForCorrection, returnedDoc!.Status);
        Assert.False(returnedDoc.IsBodyLocked);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var resubmitted = await _client.PutAsJsonAsync($"/api/documents/{document.Id}", new UpdateDocumentRequest(
            "Finance Leadership",
            "Return path test",
            "<p>Corrected body</p>",
            null,
            DocumentPriority.Normal,
            [],
            adHocApprovers,
            true,
            "return-key-2"));
        if (!resubmitted.IsSuccessStatusCode)
        {
            Assert.Fail($"Resubmit failed: {await resubmitted.Content.ReadAsStringAsync()}");
        }
        var restarted = await resubmitted.Content.ReadFromJsonAsync<DocumentDto>(_jsonOptions);
        Assert.Equal(DocumentStatus.InApproval, restarted!.Status);
        Assert.Equal(WorkflowStepStatus.Active, restarted.WorkflowSteps.OrderBy(s => s.StepOrder).First().Status);
    }

    private async Task ResetDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WdasDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    }

    private async Task<(string Token, string UserId)> LoginAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
        return (payload!.AccessToken, payload.User.Id);
    }
}
