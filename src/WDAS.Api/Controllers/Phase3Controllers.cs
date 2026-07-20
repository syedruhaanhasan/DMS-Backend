using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WDAS.Application.Models;
using WDAS.Application.Services;

namespace WDAS.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly AuditService _auditService;

    public AuditController(AuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpPost("export")]
    public async Task<ActionResult<AuditExportResult>> Export([FromBody] AuditExportRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _auditService.ExportAsync(request, cancellationToken));
    }
}

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;

    public SearchController(SearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<ActionResult<SearchResultDto>> Search(
        [FromQuery] SearchRequest request,
        [FromQuery] bool repositoryOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (repositoryOnly)
        {
            request = request with { RepositoryOnly = true };
        }

        return Ok(await _searchService.SearchAsync(request, cancellationToken));
    }
}

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ReportingService _reportingService;

    public ReportsController(ReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet("approval-times")]
    public async Task<ActionResult<IReadOnlyList<ApprovalTimeReportDto>>> ApprovalTimes(
        [FromQuery] int? departmentId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        return Ok(await _reportingService.GetApprovalTimesAsync(departmentId, fromUtc, toUtc, cancellationToken));
    }

    [HttpGet("bottlenecks")]
    public async Task<ActionResult<IReadOnlyList<BottleneckReportDto>>> Bottlenecks(
        [FromQuery] int? departmentId,
        CancellationToken cancellationToken)
    {
        return Ok(await _reportingService.GetBottlenecksAsync(departmentId, cancellationToken));
    }

    [HttpGet("volume-trends")]
    public async Task<ActionResult<IReadOnlyList<VolumeTrendReportDto>>> VolumeTrends(
        [FromQuery] int months = 6,
        [FromQuery] int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _reportingService.GetVolumeTrendsAsync(months, departmentId, cancellationToken));
    }

    [HttpGet("success-metrics")]
    public async Task<ActionResult<SuccessMetricsDto>> SuccessMetrics(
        [FromQuery] int? departmentId,
        CancellationToken cancellationToken)
    {
        return Ok(await _reportingService.GetSuccessMetricsAsync(departmentId, cancellationToken));
    }
}

[ApiController]
[Route("api/mobile")]
[Authorize]
public class MobileController : ControllerBase
{
    private readonly MobileService _mobileService;

    public MobileController(MobileService mobileService)
    {
        _mobileService = mobileService;
    }

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        await _mobileService.RegisterDeviceAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("documents/{documentId:int}/step-up")]
    public async Task<ActionResult<StepUpChallengeDto>> GetStepUpRequirement(int documentId, CancellationToken cancellationToken)
    {
        return Ok(await _mobileService.GetStepUpRequirementAsync(documentId, cancellationToken));
    }
}
