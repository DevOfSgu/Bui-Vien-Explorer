using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TravelSystem.Web.Services;

namespace TravelSystem.Web.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(AuthenticationSchemes = "VendorAuth", Roles = "Vendor")]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Poll(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : null;

        if (!userId.HasValue)
        {
            return Json(new { items = Array.Empty<object>() });
        }

        var items = await _notificationService.PollUnreadAsync("Vendor", userId, cancellationToken: cancellationToken);
        return Json(new { items });
    }
}
