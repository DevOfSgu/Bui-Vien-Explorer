namespace TravelSystem.Shared.Models;

public class Zone
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int Radius { get; set; }
    public int OrderIndex { get; set; }
    public int ZoneType { get; set; }
    public int? ShopId { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; } = false;  // Khóa chỉnh sửa
    public bool IsHidden { get; set; } = false;  // Ẩn khỏi ứng dụng
    public string? LockReason { get; set; }      // Lý do khóa
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}