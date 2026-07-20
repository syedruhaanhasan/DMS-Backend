using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Audit;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class UserTypeService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;

    public UserTypeService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        IAuditWriter auditWriter)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<UserTypeDto>> ListAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var types = _db.UserTypes.AsNoTracking();

        if (isActive == true)
        {
            types = types.Where(t => t.IsActive);
        }
        else if (isActive == false)
        {
            types = types.Where(t => !t.IsActive);
        }

        return await types
            .OrderBy(t => t.Name)
            .Select(t => new UserTypeDto(IdParsing.ToApi(t.Id), t.Name, t.Code, t.Description, t.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserTypeDto> CreateAsync(CreateUserTypeRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var name = request.Name.Trim();
        var code = NormalizeCode(request.Code);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("User type name is required.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
        {
            throw new DomainException("User type code must be at least 2 characters.");
        }

        if (await _db.UserTypes.AnyAsync(t => t.Code == code, cancellationToken))
        {
            throw new ConflictException("code_taken", "A user type with this code already exists.");
        }

        var now = _clock.UtcNow;
        var userType = new UserType
        {
            Name = name,
            Code = code,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
        };

        _db.Add(userType);
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "User type created",
            ActorUserId: _currentUser.UserId,
            EntityType: "UserType",
            EntityId: userType.Id.ToString(),
            DetailsJson: AuditDetailsBuilder.Create()
                .Set("userTypeId", userType.Id)
                .TrackCreated("Name", userType.Name)
                .TrackCreated("Code", userType.Code)
                .ToJson()),
            cancellationToken);

        return MapUserType(userType);
    }

    public async Task<UserTypeDto> UpdateAsync(int userTypeId, UpdateUserTypeRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var userType = await _db.UserTypes.FirstOrDefaultAsync(t => t.Id == userTypeId, cancellationToken)
            ?? throw new DomainException("User type not found.");

        var oldName = userType.Name;
        var oldDescription = userType.Description;
        var oldActive = userType.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            userType.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            userType.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.IsActive.HasValue)
        {
            userType.IsActive = request.IsActive.Value;
        }

        userType.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "User type updated",
            ActorUserId: _currentUser.UserId,
            EntityType: "UserType",
            EntityId: userTypeId.ToString(),
            DetailsJson: AuditDetailsBuilder.Create()
                .Set("userTypeId", userTypeId)
                .Track("Name", oldName, userType.Name)
                .Track("Description", oldDescription, userType.Description)
                .Track("Active", oldActive, userType.IsActive)
                .ToJson()),
            cancellationToken);

        return MapUserType(userType);
    }

    public async Task DeleteAsync(int userTypeId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var userType = await _db.UserTypes.FirstOrDefaultAsync(t => t.Id == userTypeId, cancellationToken)
            ?? throw new DomainException("User type not found.");

        if (await _db.Users.AnyAsync(u => u.UserTypeId == userTypeId, cancellationToken))
        {
            throw new DomainException("Cannot delete a user type that is assigned to users. Deactivate it instead.");
        }

        userType.IsActive = false;
        userType.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "User type deactivated",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { userTypeId })),
            cancellationToken);
    }

    private void EnsureCanManage()
    {
        if (!_currentUser.IsInRole(RoleNames.SuperAdmin) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.Users) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.UsersMake) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.UsersCheck))
        {
            throw new DomainException("You do not have permission to manage user types.");
        }
    }

    private static string NormalizeCode(string code) =>
        new string(code.Trim().Where(c => char.IsLetterOrDigit(c)).ToArray());

    private static UserTypeDto MapUserType(UserType userType) =>
        new(
            IdParsing.ToApi(userType.Id),
            userType.Name,
            userType.Code,
            userType.Description,
            userType.IsActive);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
