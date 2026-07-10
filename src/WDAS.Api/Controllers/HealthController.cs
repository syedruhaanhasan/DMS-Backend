using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Options;
using WDAS.Domain.Enums;

namespace WDAS.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IAttachmentScanner _scanner;
    private readonly AttachmentOptions _options;

    public HealthController(IAttachmentScanner scanner, IOptions<AttachmentOptions> options)
    {
        _scanner = scanner;
        _options = options.Value;
    }

    [HttpGet("attachment-scan")]
    [Authorize(Policy = "WorkflowAdmin")]
    public async Task<ActionResult<object>> AttachmentScan(CancellationToken cancellationToken)
    {
        if (!_options.VirusScanEnabled)
        {
            return Ok(new
            {
                enabled = false,
                provider = "DevAttachmentScanner",
                status = "disabled",
                message = "Set Attachments:VirusScanEnabled=true to use ClamAV.",
            });
        }

        await using var stream = new MemoryStream("WDAS health-check"u8.ToArray());
        var result = await _scanner.ScanAsync(stream, "health-check.txt", cancellationToken);

        return Ok(new
        {
            enabled = true,
            provider = "ClamAV",
            host = _options.ClamAvHost,
            port = _options.ClamAvPort,
            status = result == AttachmentScanStatus.Clean ? "healthy" : result.ToString().ToLowerInvariant(),
            failUploadWhenUnavailable = _options.FailUploadWhenScannerUnavailable,
        });
    }
}
