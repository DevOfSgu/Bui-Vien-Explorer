using SQLite;

namespace TravelSystem.Shared.Models;

[Table("LocalFavorite")]
public class LocalFavorite
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string GuestId { get; set; } = string.Empty;

    public int ZoneId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 0 = not deleted, 1 = deleted
    [Indexed]
    public int IsDeleted { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
