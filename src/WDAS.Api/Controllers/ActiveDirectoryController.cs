using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WDAS.Application.Models;
using WDAS.Application.Services;

namespace WDAS.Api.Controllers;

[ApiController]
[Route("api/config/active-directory")]
[Authorize]
public class ActiveDirectoryController : ControllerBase
{
    private readonly ActiveDirectorySettingsService _service;

    public ActiveDirectoryController(ActiveDirectorySettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "perm:config.ad")]
    public async Task<ActionResult<ActiveDirectorySettingsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetAsync(cancellationToken));
    }

    /// <summary>Enabled flag only — available to any authenticated user (login gating, user creation UI).</summary>
    [HttpGet("status")]
    public async Task<ActionResult<ActiveDirectoryStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetStatusAsync(cancellationToken));
    }

    [HttpPut]
    [Authorize(Policy = "perm:config.ad.write")]
    public async Task<ActionResult<ActiveDirectorySettingsDto>> Update(
        [FromBody] UpdateActiveDirectorySettingsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdateAsync(request, cancellationToken));
    }
}
