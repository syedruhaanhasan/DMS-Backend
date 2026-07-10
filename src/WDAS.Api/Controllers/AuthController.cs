using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WDAS.Application.Models;
using WDAS.Application.Services;

namespace WDAS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.LoginAsync(request, cancellationToken));
    }

    [HttpPost("sync")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<SyncResultDto>> Sync(CancellationToken cancellationToken)
    {
        return Ok(await _authService.SyncDirectoryAsync(cancellationToken));
    }
}

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly UserPreferencesService _userPreferencesService;

    public UsersController(AuthService authService, UserPreferencesService userPreferencesService)
    {
        _authService = authService;
        _userPreferencesService = userPreferencesService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> GetUsers(CancellationToken cancellationToken)
    {
        return Ok(await _authService.GetUsersAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<UserSummaryDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUsers), new { }, user);
    }

    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<UserSummaryDto>> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.UpdateUserRoleAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        await _authService.DeleteUserAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("me/preferences")]
    public async Task<ActionResult<UserPreferencesDto>> GetMyPreferences(CancellationToken cancellationToken)
    {
        return Ok(await _userPreferencesService.GetMyPreferencesAsync(cancellationToken));
    }

    [HttpPut("me/preferences")]
    public async Task<ActionResult<UserPreferencesDto>> UpdateMyPreferences(
        [FromBody] UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _userPreferencesService.UpdateMyPreferencesAsync(request, cancellationToken));
    }
}

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly AuthService _authService;

    public DepartmentsController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetDepartments(CancellationToken cancellationToken)
    {
        return Ok(await _authService.GetDepartmentsAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<DepartmentDto>> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _authService.CreateDepartmentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDepartments), new { }, department);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<DepartmentDto>> UpdateDepartment(Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.UpdateDepartmentAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> DeleteDepartment(Guid id, CancellationToken cancellationToken)
    {
        await _authService.DeleteDepartmentAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/document-types")]
[Authorize]
public class DocumentTypesController : ControllerBase
{
    private readonly DocumentTypeService _documentTypeService;

    public DocumentTypesController(DocumentTypeService documentTypeService)
    {
        _documentTypeService = documentTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentTypeDto>>> GetDocumentTypes([FromQuery] string? query, CancellationToken cancellationToken)
    {
        return Ok(await _documentTypeService.ListAsync(query, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<DocumentTypeDto>> CreateDocumentType([FromBody] CreateDocumentTypeRequest request, CancellationToken cancellationToken)
    {
        var documentType = await _documentTypeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDocumentTypes), new { }, documentType);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<ActionResult<DocumentTypeDto>> UpdateDocumentType(Guid id, [FromBody] UpdateDocumentTypeRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _documentTypeService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> DeleteDocumentType(Guid id, CancellationToken cancellationToken)
    {
        await _documentTypeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
