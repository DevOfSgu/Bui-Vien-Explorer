namespace TravelSystem.Web.Services;

public interface INotificationService
{
    Task NotifyAdminsAsync(string message, string? linkUrl = null, CancellationToken cancellationToken = default);
    Task NotifyVendorAsync(int vendorUserId, string message, string? linkUrl = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationItemDto>> PollUnreadAsync(string role, int? userId, int take = 5, CancellationToken cancellationToken = default);
}

public record NotificationItemDto(int Id, string Message, string? LinkUrl, DateTime CreatedAt);
