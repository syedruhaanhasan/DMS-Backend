using System.IdentityModel.Tokens.Jwt;
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

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        // Prefer the token's own expiry so the denylist entry lives exactly as long as needed.
        var expiresAtUtc = DateTime.UtcNow.AddHours(8);
        var expClaim = User.FindFirst("exp")?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (long.TryParse(expClaim, out var expSeconds))
        {
            expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
        }

        await _authService.LogoutAsync(jti, expiresAtUtc, cancellationToken);
        return NoContent();
    }

    [HttpPost("sync")]
    [Authorize(Policy = "perm:config.ad.check")]
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
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> GetUsers([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        return Ok(await _authService.GetUsersAsync(isActive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "perm:config.users.make")]
    public async Task<ActionResult<UserSummaryDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUsers), new { }, user);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "perm:config.users.check")]
    public async Task<ActionResult<UserSummaryDto>> UpdateUser(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.UpdateUserAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:int}/role")]
    [Authorize(Policy = "perm:config.users.check")]
    public async Task<ActionResult<UserSummaryDto>> UpdateRole(int id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.UpdateUserRoleAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Policy = "perm:config.users.check")]
    public async Task<ActionResult<UserSummaryDto>> SetUserStatus(int id, [FromBody] SetActiveStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.SetUserActiveStatusAsync(id, request.IsActive, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "perm:config.users.check")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
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
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetDepartments([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        return Ok(await _authService.GetDepartmentsAsync(isActive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "perm:config.departments.make")]
    public async Task<ActionResult<DepartmentDto>> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _authService.CreateDepartmentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDepartments), new { }, department);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "perm:config.departments.write")]
    public async Task<ActionResult<DepartmentDto>> UpdateDepartment(int id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.UpdateDepartmentAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "perm:config.departments.check")]
    public async Task<IActionResult> DeleteDepartment(int id, CancellationToken cancellationToken)
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
    public async Task<ActionResult<IReadOnlyList<DocumentTypeDto>>> GetDocumentTypes([FromQuery] string? query, [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        return Ok(await _documentTypeService.ListAsync(query, isActive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "perm:config.document_types.make")]
    public async Task<ActionResult<DocumentTypeDto>> CreateDocumentType([FromBody] CreateDocumentTypeRequest request, CancellationToken cancellationToken)
    {
        var documentType = await _documentTypeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDocumentTypes), new { }, documentType);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "perm:config.document_types.write")]
    public async Task<ActionResult<DocumentTypeDto>> UpdateDocumentType(int id, [FromBody] UpdateDocumentTypeRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _documentTypeService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "perm:config.document_types.check")]
    public async Task<IActionResult> DeleteDocumentType(int id, CancellationToken cancellationToken)
    {
        await _documentTypeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/user-types")]
[Authorize]
public class UserTypesController : ControllerBase
{
    private readonly UserTypeService _userTypeService;

    public UserTypesController(UserTypeService userTypeService)
    {
        _userTypeService = userTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserTypeDto>>> GetUserTypes([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        return Ok(await _userTypeService.ListAsync(isActive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "perm:config.users.make")]
    public async Task<ActionResult<UserTypeDto>> CreateUserType([FromBody] CreateUserTypeRequest request, CancellationToken cancellationToken)
    {
        var userType = await _userTypeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUserTypes), new { }, userType);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "perm:config.users.check")]
    public async Task<ActionResult<UserTypeDto>> UpdateUserType(int id, [FromBody] UpdateUserTypeRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _userTypeService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "perm:config.users.check")]
    public async Task<IActionResult> DeleteUserType(int id, CancellationToken cancellationToken)
    {
        await _userTypeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
