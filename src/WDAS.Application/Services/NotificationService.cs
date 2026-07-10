using Microsoft.EntityFrameworkCore;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class NotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public NotificationService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
        {
            throw new DomainException("Authentication required.");
        }

        return await _db.Notifications
            .Where(n => n.RecipientUserId == userId && n.Channel == NotificationChannel.InApp)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .Select(n => new NotificationDto(
                n.Id,
                n.EventType.ToString(),
                n.Channel.ToString(),
                n.Subject,
                n.Body,
                n.DocumentId,
                n.CreatedAtUtc,
                n.ReadAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
        {
            throw new DomainException("Authentication required.");
        }

        var notification = await _db.Notifications.FirstOrDefaultAsync(
            n => n.Id == notificationId && n.RecipientUserId == userId,
            cancellationToken)
            ?? throw new DomainException("Notification not found.");

        notification.ReadAtUtc ??= DateTime.UtcNow;
        await SaveAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
        {
            throw new DomainException("Authentication required.");
        }

        var now = DateTime.UtcNow;
        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == userId && n.Channel == NotificationChannel.InApp && n.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var n in unread)
        {
            n.ReadAtUtc = now;
        }

        await SaveAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
