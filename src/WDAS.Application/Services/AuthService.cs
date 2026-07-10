using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Common;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class AuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityProvider _identityProvider;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(
        IApplicationDbContext db,
        IIdentityProvider identityProvider,
        IJwtTokenService jwtTokenService,
        IClock clock,
        IAuditWriter auditWriter,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher)
    {
        _db = db;
        _identityProvider = identityProvider;
        _jwtTokenService = jwtTokenService;
        _clock = clock;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        User? user = null;

        var authenticated = await _identityProvider.AuthenticateAsync(request.Username, request.Password, cancellationToken);
        if (authenticated is not null)
        {
            user = await _db.Users
                .Include(u => u.Department)
                .Include(u => u.RoleMappings)
                .FirstOrDefaultAsync(u => u.AdObjectId == authenticated.AdObjectId, cancellationToken);
        }
        else
        {
            user = await _db.Users
                .Include(u => u.Department)
                .Include(u => u.RoleMappings)
                .FirstOrDefaultAsync(
                    u => u.UserPrincipalName == request.Username && u.PasswordHash != null,
                    cancellationToken);

            if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash!))
            {
                throw new DomainException("Invalid username or password.");
            }
        }

        if (user is null || user.IsDisabledInApp || !user.IsEnabledInAd)
        {
            throw new DomainException("User is not authorized for this application.");
        }

        var roles = user.RoleMappings.Select(r => r.Role).Distinct().ToList();
        if (roles.Contains(ApplicationRole.SuperAdmin))
        {
            roles = Enum.GetValues<ApplicationRole>().ToList();
        }

        var tokenUser = new AuthenticatedUser(user.Id, user.AdObjectId, user.DisplayName, user.Email, user.DepartmentId, roles);
        var token = _jwtTokenService.CreateToken(tokenUser);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Login,
            "User login",
            ActorUserId: user.Id,
            ActorDisplayName: user.DisplayName,
            ActorEmail: user.Email),
            cancellationToken);

        return new LoginResponse(token, _clock.UtcNow.AddHours(8), MapUser(user));
    }

    public async Task<UserSummaryDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var username = request.UserPrincipalName.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Username is required.");
        }

        var isLocalAccount = request.AccountType == UserAccountType.Local;
        if (isLocalAccount)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                throw new DomainException("Password must be at least 8 characters.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new DomainException("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new DomainException("Email is required.");
        }

        var email = request.Email.Trim();
        if (!EmailFormatValidator.IsValid(email))
        {
            throw new DomainException("Invalid email format.");
        }

        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken)
            ?? throw new DomainException("Department not found.");

        if (await _db.Users.AnyAsync(u => u.UserPrincipalName == username, cancellationToken))
        {
            throw new ConflictException("username_taken", "A user with this username already exists.");
        }

        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("email_taken", "A user with this email already exists.");
        }

        string adObjectId;
        if (isLocalAccount)
        {
            adObjectId = $"local-{Guid.NewGuid():N}";
        }
        else
        {
            adObjectId = string.IsNullOrWhiteSpace(request.AdObjectId)
                ? $"ad-{username.ToLowerInvariant()}"
                : request.AdObjectId.Trim();

            if (await _db.Users.AnyAsync(u => u.AdObjectId == adObjectId, cancellationToken))
            {
                throw new ConflictException("ad_object_taken", "A user with this AD Object ID already exists.");
            }
        }

        var now = _clock.UtcNow;
        var user = new User
        {
            AdObjectId = adObjectId,
            UserPrincipalName = username,
            DisplayName = request.DisplayName.Trim(),
            Email = email,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Staff" : request.Title.Trim(),
            DepartmentId = department.Id,
            PasswordHash = isLocalAccount ? _passwordHasher.Hash(request.Password!) : null,
            IsEnabledInAd = true,
            CreatedAtUtc = now,
            LastSyncedAtUtc = now
        };

        var roles = ResolveRoles(request.Roles, request.Role);
        if (roles.Count == 0)
        {
            throw new DomainException("At least one role is required.");
        }

        foreach (var role in roles)
        {
            user.RoleMappings.Add(new RoleMapping
            {
                Role = role,
                DepartmentId = role == ApplicationRole.SuperAdmin ? null : department.Id,
                CreatedAtUtc = now
            });
        }

        _db.Add(user);

        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "User created",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { user.Id, user.UserPrincipalName, Roles = roles, request.AccountType })),
            cancellationToken);

        user = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.RoleMappings)
            .FirstAsync(u => u.Id == user.Id, cancellationToken);

        return MapUser(user);
    }

    public async Task<UserSummaryDto> UpdateUserRoleAsync(Guid userId, UpdateUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var user = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.RoleMappings)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new DomainException("User not found.");

        var roles = ResolveRoles(request.Roles, request.Role);
        if (roles.Count == 0)
        {
            throw new DomainException("At least one role is required.");
        }

        var now = _clock.UtcNow;
        _db.RemoveRange(user.RoleMappings);
        foreach (var role in roles)
        {
            _db.Add(new RoleMapping
            {
                UserId = user.Id,
                Role = role,
                DepartmentId = role == ApplicationRole.SuperAdmin ? null : user.DepartmentId,
                CreatedAtUtc = now
            });
        }

        user.UpdatedAtUtc = now;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "User roles updated",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { userId, Roles = roles })),
            cancellationToken);

        user = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.RoleMappings)
            .FirstAsync(u => u.Id == userId, cancellationToken);

        return MapUser(user);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        if (userId == _currentUser.UserId)
        {
            throw new DomainException("You cannot delete your own account.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new DomainException("User not found.");

        user.IsDisabledInApp = true;
        user.IsEnabledInAd = false;
        user.AdDisabledAtUtc = _clock.UtcNow;
        user.UpdatedAtUtc = _clock.UtcNow;

        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "User disabled",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { userId })),
            cancellationToken);
    }

    public async Task<UserSummaryDto> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        if (!isActive && userId == _currentUser.UserId)
        {
            throw new DomainException("You cannot deactivate your own account.");
        }

        var user = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.RoleMappings)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new DomainException("User not found.");

        user.IsDisabledInApp = !isActive;
        if (!isActive)
        {
            user.AdDisabledAtUtc = _clock.UtcNow;
        }
        else
        {
            user.AdDisabledAtUtc = null;
        }

        user.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            isActive ? "User activated" : "User deactivated",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { userId, isActive })),
            cancellationToken);

        return MapUser(user);
    }

    public async Task<SyncResultDto> SyncDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var departments = await _identityProvider.GetDirectoryDepartmentsAsync(cancellationToken);
        var users = await _identityProvider.GetDirectoryUsersAsync(cancellationToken);
        var now = _clock.UtcNow;

        foreach (var dept in departments)
        {
            var existing = await _db.Departments.FirstOrDefaultAsync(d => d.AdObjectId == dept.AdObjectId, cancellationToken);
            if (existing is null)
            {
                _db.Add(new Department
                {
                    AdObjectId = dept.AdObjectId,
                    Name = dept.Name,
                    Code = dept.Code,
                    IsActive = true,
                    CreatedAtUtc = now,
                    LastSyncedAtUtc = now
                });
            }
            else
            {
                existing.Name = dept.Name;
                existing.Code = dept.Code;
                existing.LastSyncedAtUtc = now;
                existing.UpdatedAtUtc = now;
            }
        }

        await SaveAsync(cancellationToken);

        var departmentMap = await _db.Departments.ToDictionaryAsync(d => d.AdObjectId, cancellationToken);

        foreach (var dept in departments.Where(d => d.ParentAdObjectId is not null))
        {
            if (!departmentMap.TryGetValue(dept.AdObjectId, out var child) ||
                !departmentMap.TryGetValue(dept.ParentAdObjectId!, out var parent))
            {
                continue;
            }

            child.ParentDepartmentId = parent.Id;
            child.UpdatedAtUtc = now;
        }

        foreach (var snapshot in users)
        {
            if (!departmentMap.TryGetValue(snapshot.DepartmentAdObjectId, out var department))
            {
                continue;
            }

            var existing = await _db.Users.FirstOrDefaultAsync(u => u.AdObjectId == snapshot.AdObjectId, cancellationToken);
            if (existing is null)
            {
                _db.Add(new User
                {
                    AdObjectId = snapshot.AdObjectId,
                    UserPrincipalName = snapshot.UserPrincipalName,
                    DisplayName = snapshot.DisplayName,
                    Email = snapshot.Email,
                    Title = snapshot.Title,
                    DepartmentId = department.Id,
                    IsEnabledInAd = snapshot.IsEnabled,
                    CreatedAtUtc = now,
                    LastSyncedAtUtc = now
                });
            }
            else
            {
                existing.UserPrincipalName = snapshot.UserPrincipalName;
                existing.DisplayName = snapshot.DisplayName;
                existing.Email = snapshot.Email;
                existing.Title = snapshot.Title;
                existing.DepartmentId = department.Id;
                existing.IsEnabledInAd = snapshot.IsEnabled;
                existing.LastSyncedAtUtc = now;
                existing.UpdatedAtUtc = now;

                if (!snapshot.IsEnabled && existing.AdDisabledAtUtc is null)
                {
                    existing.AdDisabledAtUtc = now;
                }
            }
        }

        await SaveAsync(cancellationToken);

        var userMap = await _db.Users.ToDictionaryAsync(u => u.AdObjectId, cancellationToken);
        foreach (var snapshot in users.Where(u => u.ManagerAdObjectId is not null))
        {
            if (!userMap.TryGetValue(snapshot.AdObjectId, out var user) ||
                !userMap.TryGetValue(snapshot.ManagerAdObjectId!, out var manager))
            {
                continue;
            }

            user.ManagerUserId = manager.Id;
            user.UpdatedAtUtc = now;
        }

        await SaveAsync(cancellationToken);

        return new SyncResultDto(users.Count, departments.Count, now, _identityProvider.GetType().Name);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Users
            .Include(u => u.Department)
            .Include(u => u.RoleMappings)
            .AsQueryable();

        if (isActive == true)
        {
            query = query.Where(u => !u.IsDisabledInApp);
        }
        else if (isActive == false)
        {
            query = query.Where(u => u.IsDisabledInApp);
        }

        var users = await query
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        return users.Select(MapUser).ToList();
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Departments.AsQueryable();

        if (isActive == true)
        {
            query = query.Where(d => d.IsActive);
        }
        else if (isActive == false)
        {
            query = query.Where(d => !d.IsActive);
        }

        return await query
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.ParentDepartmentId, d.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Department name is required.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
        {
            throw new DomainException("Department code must be at least 2 characters.");
        }

        if (await _db.Departments.AnyAsync(d => d.Code == code, cancellationToken))
        {
            throw new ConflictException("code_taken", "A department with this code already exists.");
        }

        if (request.ParentDepartmentId is Guid parentId)
        {
            var parentExists = await _db.Departments.AnyAsync(d => d.Id == parentId, cancellationToken);
            if (!parentExists)
            {
                throw new DomainException("Parent department not found.");
            }
        }

        var now = _clock.UtcNow;
        var department = new Department
        {
            AdObjectId = $"local-dept-{Guid.NewGuid():N}",
            Name = name,
            Code = code,
            ParentDepartmentId = request.ParentDepartmentId,
            IsActive = true,
            CreatedAtUtc = now,
            LastSyncedAtUtc = now
        };

        _db.Add(department);
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "Department created",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { department.Id, department.Name, department.Code })),
            cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Code, department.ParentDepartmentId, department.IsActive);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid departmentId, UpdateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            ?? throw new DomainException("Department not found.");

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            department.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var code = request.Code.Trim().ToUpperInvariant();
            if (code.Length < 2)
            {
                throw new DomainException("Department code must be at least 2 characters.");
            }

            if (await _db.Departments.AnyAsync(d => d.Code == code && d.Id != departmentId, cancellationToken))
            {
                throw new ConflictException("code_taken", "A department with this code already exists.");
            }

            department.Code = code;
        }

        if (request.IsActive.HasValue)
        {
            department.IsActive = request.IsActive.Value;
        }

        department.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "Department updated",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { department.Id, department.Name, department.Code, department.IsActive })),
            cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Code, department.ParentDepartmentId, department.IsActive);
    }

    public async Task DeleteDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            ?? throw new DomainException("Department not found.");

        if (await _db.Users.AnyAsync(u => u.DepartmentId == departmentId && !u.IsDisabledInApp, cancellationToken))
        {
            throw new DomainException("Cannot delete a department that still has active users. Deactivate it instead.");
        }

        if (await _db.Workflows.AnyAsync(w => w.DepartmentId == departmentId && w.IsActive, cancellationToken))
        {
            throw new DomainException("Cannot delete a department that still has active workflows. Deactivate it instead.");
        }

        department.IsActive = false;
        department.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "Department deactivated",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { departmentId })),
            cancellationToken);
    }

    private static List<ApplicationRole> ResolveRoles(List<ApplicationRole>? roles, ApplicationRole? legacyRole)
    {
        IEnumerable<ApplicationRole> source = roles is { Count: > 0 }
            ? roles
            : legacyRole is not null && legacyRole.Value != default
                ? new[] { legacyRole.Value }
                : Array.Empty<ApplicationRole>();

        return source
            .Where(r => Enum.IsDefined(r))
            .Distinct()
            .ToList();
    }

    private void EnsureSuperAdmin()
    {
        if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            throw new DomainException("Only Super Admin can manage users.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static UserSummaryDto MapUser(User user) =>
        new(
            user.Id,
            user.AdObjectId,
            user.UserPrincipalName,
            user.DisplayName,
            user.Email,
            user.Title,
            user.DepartmentId,
            user.Department.Name,
            user.RoleMappings.Select(r => r.Role).Distinct().ToList(),
            !user.IsDisabledInApp);
}
