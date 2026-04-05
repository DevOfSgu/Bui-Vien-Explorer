namespace TravelSystem.Web.Models;

public class AppNotification
{
    public int Id { get; set; }
    public int? RecipientUserId { get; set; }
    public string RecipientRole { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
