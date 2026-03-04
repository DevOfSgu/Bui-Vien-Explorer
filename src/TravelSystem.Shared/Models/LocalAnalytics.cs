using SQLite;

namespace TravelSystem.Shared.Models
{
    /// <summary>
    /// Lưu dữ liệu analytics ẩn danh trên thiết bị Mobile, chờ đồng bộ lên Web Server.
    /// Phục vụ 4 yêu cầu: Tuyến di chuyển, Top POI, Thời gian trung bình, Heatmap.
    /// </summary>
    [Table("LocalAnalytics")]
    public class LocalAnalytics
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // UUID ẩn danh, sinh 1 lần khi cài app để theo dõi 1 "chuyến đi"
        public string SessionId { get; set; } = string.Empty;

        // FK đến Zone và Route (lưu dạng string ID từ server)
        public string ZoneId { get; set; } = string.Empty;   // Nullable: rỗng khi ActionType = "LocationPing"
        public string RouteId { get; set; } = string.Empty;

        // Cần cho Heatmap vị trí người dùng
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Loại sự kiện: "EnterZone", "PlayNarration", "LocationPing"
        public string ActionType { get; set; } = string.Empty;

        // Thời gian ở lại POI (giây) → Tính thời gian trung bình nghe
        public int DwellTimeSeconds { get; set; }

        // Thời điểm ghi sự kiện (ISO 8601)
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        // Trạng thái đồng bộ: 0 = chưa gửi server, 1 = đã gửi
        [Indexed]
        public int IsSynced { get; set; } = 0;
    }
}
