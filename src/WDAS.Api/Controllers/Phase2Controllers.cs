using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WDAS.Application.Models;
using WDAS.Application.Services;

namespace WDAS.Api.Controllers;

[ApiController]
[Route("api/documents/{documentId:guid}/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly AttachmentService _attachmentService;

    public AttachmentsController(AttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> List(Guid documentId, CancellationToken cancellationToken)
    {
        return Ok(await _attachmentService.ListForDocumentAsync(documentId, cancellationToken));
    }

    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(
        Guid documentId,
        IFormFile file,
        [FromForm] string? logicalName,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "File is required." });
        }

        await using var stream = file.OpenReadStream();
        var attachment = await _attachmentService.UploadAsync(
            documentId,
            stream,
            file.FileName,
            file.ContentType,
            logicalName,
            null,
            cancellationToken);

        return CreatedAtAction(nameof(AttachmentPreviewController.Preview), "AttachmentPreview", new { id = attachment.Id }, attachment);
    }
}

[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentPreviewController : ControllerBase
{
    private readonly AttachmentService _attachmentService;

    public AttachmentPreviewController(AttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var (content, contentType, fileName) = await _attachmentService.GetPreviewAsync(id, cancellationToken);
        return File(content, contentType, fileName);
    }
}

[ApiController]
[Route("api/repository")]
[Authorize]
public class RepositoryController : ControllerBase
{
    private readonly FinalizationService _finalizationService;

    public RepositoryController(FinalizationService finalizationService)
    {
        _finalizationService = finalizationService;
    }

    [HttpGet("{documentId}")]
    public async Task<ActionResult<RepositoryDocumentDto>> Get(string documentId, CancellationToken cancellationToken)
    {
        return Ok(await _finalizationService.GetRepositoryDocumentAsync(documentId, cancellationToken));
    }

    [HttpGet("{documentId}/download")]
    public async Task<IActionResult> Download(string documentId, [FromQuery] string format = "pdf", CancellationToken cancellationToken = default)
    {
        var (content, contentType, fileName) = await _finalizationService.DownloadArchiveAsync(documentId, format, cancellationToken);
        return File(content, contentType, fileName);
    }
}

[ApiController]
[Route("api/delegations")]
[Authorize]
public class DelegationsController : ControllerBase
{
    private readonly DelegationService _delegationService;

    public DelegationsController(DelegationService delegationService)
    {
        _delegationService = delegationService;
    }

    [HttpPost]
    public async Task<ActionResult<DelegationDto>> Create([FromBody] CreateDelegationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _delegationService.CreateDelegationAsync(request, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DelegationDto>>> List([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        return Ok(await _delegationService.ListDelegationsAsync(isActive, cancellationToken));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<DelegationDto>> SetStatus(Guid id, [FromBody] SetActiveStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _delegationService.SetDelegationActiveStatusAsync(id, request.IsActive, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _delegationService.DeactivateDelegationAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/external-approvers")]
[Authorize]
public class ExternalApproversController : ControllerBase
{
    private readonly ExternalApproverService _externalApproverService;

    public ExternalApproversController(ExternalApproverService externalApproverService)
    {
        _externalApproverService = externalApproverService;
    }

    [HttpPost]
    public async Task<ActionResult<ExternalApproverSessionDto>> Create(
        [FromBody] CreateExternalApproverRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _externalApproverService.CreateExternalApproverAsync(request, cancellationToken));
    }

    [HttpGet]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<IReadOnlyList<ExternalApproverListItemDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _externalApproverService.ListExternalApproversAsync(cancellationToken));
    }

    [HttpPost("{id:guid}/resend")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<ExternalApproverSessionDto>> Resend(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _externalApproverService.ResendLinkAsync(id, cancellationToken));
    }
}

[ApiController]
[Route("api/external-sessions")]
public class ExternalSessionsController : ControllerBase
{
    private readonly ExternalApproverService _externalApproverService;

    public ExternalSessionsController(ExternalApproverService externalApproverService)
    {
        _externalApproverService = externalApproverService;
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<ActionResult<ExternalSessionDto>> VerifyOtp(
        [FromBody] VerifyExternalOtpRequest request,
        CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(await _externalApproverService.VerifyOtpAsync(
            request with { ClientIp = clientIp },
            cancellationToken));
    }
}

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<PersonalDashboardDto>> GetPersonal(CancellationToken cancellationToken)
    {
        return Ok(await _dashboardService.GetPersonalDashboardAsync(cancellationToken));
    }

    [HttpGet("department/{id:guid}")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<DepartmentDashboardDto>> GetDepartment(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dashboardService.GetDepartmentDashboardAsync(id, cancellationToken));
    }
}

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMine(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _notificationService.GetMyNotificationsAsync(take, cancellationToken));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkReadAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}
