namespace WDAS.Application.Models;

public record UserTypeDto(
    string Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive);

public record CreateUserTypeRequest(
    string Name,
    string Code,
    string? Description);

public record UpdateUserTypeRequest(
    string? Name,
    string? Description,
    bool? IsActive);
