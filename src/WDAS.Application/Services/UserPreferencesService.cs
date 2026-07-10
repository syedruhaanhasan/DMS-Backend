using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class UserPreferencesService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UserPreferencesService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<UserPreferencesDto> GetMyPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserPreferencesDto> UpdateMyPreferencesAsync(UpdateUserPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);

        if (request.NotificationPreferences is not null)
        {
            user.NotificationPreferencesJson = request.NotificationPreferences;
        }

        if (request.OutOfOfficeMessage is not null)
        {
            user.OutOfOfficeMessage = request.OutOfOfficeMessage;
        }

        if (request.PreferredLanguage is not null)
        {
            user.PreferredLanguage = request.PreferredLanguage;
        }

        user.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(cancellationToken);
        return Map(user);
    }

    private async Task<Domain.Entities.User> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, cancellationToken)
            ?? throw new DomainException("User not found.");
    }

    private static UserPreferencesDto Map(Domain.Entities.User user) =>
        new(user.NotificationPreferencesJson, user.OutOfOfficeMessage, user.PreferredLanguage);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
