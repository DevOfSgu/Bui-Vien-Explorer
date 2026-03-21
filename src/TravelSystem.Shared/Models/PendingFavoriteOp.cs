using SQLite;

namespace TravelSystem.Shared.Models;

public enum FavoriteOperation
{
    Add = 0,
    Remove = 1
}

[Table("PendingFavoriteOp")]
public class PendingFavoriteOp
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string GuestId { get; set; } = string.Empty;

    public int ZoneId { get; set; }

    public FavoriteOperation Operation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Indexed]
    public int Processed { get; set; } = 0;

    public int AttemptCount { get; set; } = 0;

    public string? LastError { get; set; }
}
