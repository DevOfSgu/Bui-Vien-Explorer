using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;
using TravelSystem.Web.Models;

namespace TravelSystem.Web.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task NotifyAdminsAsync(string message, string? linkUrl = null, CancellationToken cancellationToken = default)
    {
        var adminIds = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == 0 && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (adminIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var notifications = adminIds.Select(adminId => new AppNotification
        {
            RecipientUserId = adminId,
            RecipientRole = "Admin",
            Message = message,
            LinkUrl = linkUrl,
            IsRead = false,
            CreatedAt = now
        });

        await _db.AppNotifications.AddRangeAsync(notifications, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyVendorAsync(int vendorUserId, string message, string? linkUrl = null, CancellationToken cancellationToken = default)
    {
        var notification = new AppNotification
        {
            RecipientUserId = vendorUserId,
            RecipientRole = "Vendor",
            Message = message,
            LinkUrl = linkUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.AppNotifications.AddAsync(notification, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationItemDto>> PollUnreadAsync(string role, int? userId, int take = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return [];
        }

        var normalizedRole = role.Trim();

        var query = _db.AppNotifications
            .Where(n => !n.IsRead && n.RecipientRole == normalizedRole);

        if (userId.HasValue)
        {
            query = query.Where(n => n.RecipientUserId == userId.Value);
        }

        var unread = await query
            .OrderBy(n => n.CreatedAt)
            .Take(Math.Clamp(take, 1, 10))
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return unread
            .Select(n => new NotificationItemDto(n.Id, n.Message, n.LinkUrl, n.CreatedAt))
            .ToList();
    }
}
