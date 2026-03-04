using SQLite;

namespace TravelSystem.Shared.Models
{
    public class VisitLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string ZoneId { get; set; }
        public string VisitedAt { get; set; } // Thời điểm ghé thăm (ISO)
        public string Language { get; set; }

        // Mã phiên ẩn danh — dùng để server deduplicate analytics
        public string SessionId { get; set; } // GUID, sinh 1 lần khi cài app

        public int NarrationPlayed { get; set; } // 0: Chưa nghe, 1: Đã nghe
        public int DwellTimeSeconds { get; set; } // Thời gian dừng chân tại zone (giây)

        [Indexed]
        public int IsSynced { get; set; } // 0: Chưa đồng bộ, 1: Đã lên server
    }
}
