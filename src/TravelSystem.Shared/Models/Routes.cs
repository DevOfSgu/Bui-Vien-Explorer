namespace TravelSystem.Shared.Models;

public class Routes
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal StartLatitude { get; set; }
    public decimal StartLongitude { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; } = false;  // Khóa chỉnh sửa
    public bool IsHidden { get; set; } = false;  // Ẩn khỏi ứng dụng
    public string? LockReason { get; set; }      // Lý do khóa
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}