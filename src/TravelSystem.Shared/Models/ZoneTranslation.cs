using System;

namespace TravelSystem.Shared.Models;

public class ZoneTranslation
{
    public int Id { get; set; }
    public int ZoneId { get; set; }
    public string Language { get; set; } = "vi";
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Zone? Zone { get; set; }
}
