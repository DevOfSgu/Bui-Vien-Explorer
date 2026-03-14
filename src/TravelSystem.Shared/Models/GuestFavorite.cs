namespace TravelSystem.Shared.Models;

public class GuestFavorite
{
    public int Id { get; set; }

    /// <summary>UUID stored in localStorage (Web) or MAUI Preferences (Mobile)</summary>
    public string GuestId { get; set; } = string.Empty;

    public int ZoneId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
