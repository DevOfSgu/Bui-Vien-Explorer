using SQLite;

namespace TravelSystem.Shared.Models
{
    public class LocalRoute
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string RouteId { get; set; } // UUID từ server

        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }

        // Tọa độ điểm bắt đầu của tour
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }

        // QR Code để kích hoạt nội dung (không cần GPS, quét là nghe)
        [Indexed]
        public string QRCode { get; set; } // UUID/mã định danh dùng trong QR

        public string SyncedAt { get; set; } // ISO datetime
        public int IsActive { get; set; } = 1; // 0 hoặc 1
    }
}
