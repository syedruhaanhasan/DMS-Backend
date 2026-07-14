using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WDAS.Application.Models;
using WDAS.Application.Services;

namespace WDAS.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly RoleService _roleService;

    public RolesController(RoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleSummaryDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _roleService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:config.roles")]
    public async Task<ActionResult<RoleDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _roleService.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "perm:config.roles.make")]
    public async Task<ActionResult<RoleDetailDto>> Create([FromBody] CreateSecurityRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = role.Id }, role);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:config.roles.make")]
    public async Task<ActionResult<RoleDetailDto>> Update(Guid id, [FromBody] UpdateSecurityRoleRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _roleService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "perm:config.roles.check")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _roleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/permissions")]
[Authorize(Policy = "perm:config.roles")]
public class PermissionsController : ControllerBase
{
    private readonly RoleService _roleService;

    public PermissionsController(RoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PermissionDefinitionDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _roleService.GetPermissionCatalogAsync(cancellationToken));
    }
}
