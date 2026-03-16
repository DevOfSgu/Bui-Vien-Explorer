using SQLite;

namespace TravelSystem.Shared.Models
{
    public class LocalZone
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string ZoneId { get; set; } = string.Empty; // UUID từ server



        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty; // Ảnh minh họa POI


        // Dữ liệu quan trọng cho Geofencing
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Radius { get; set; } // Bán kính kích hoạt (mét)

        public int OrderIndex { get; set; }
        public int ZoneType { get; set; } // Bar, Food, History...

        // Trạng thái & logic kích hoạt
        public int IsActive { get; set; } = 1;     // 0: Tắt, 1: Bật
        public int ActiveTime { get; set; } = 0;   // 0: Cả ngày, 1: Ban ngày, 2: Ban đêm

        // Để Mobile biết khi nào cần sync lại từ server
        public string UpdatedAt { get; set; } = string.Empty; // ISO datetime từ server

    }
}
