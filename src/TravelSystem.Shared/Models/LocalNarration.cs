using SQLite;

namespace TravelSystem.Shared.Models
{
    public class LocalNarration
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string ZoneId { get; set; } // Liên kết với điểm dừng

        public string Language { get; set; } // "vi", "en"...
        public string Text { get; set; }     // Script TTS (nội dung thuyết minh)
        public string VoiceId { get; set; } // Tùy chọn giọng đọc TTS

        // Hỗ trợ phát file MP3 thực tế
        public string FileUrl { get; set; }  // URL file MP3 trên server/CDN
        public string LocalFilePath { get; set; } // Đường dẫn file đã tải về thiết bị
        public int Version { get; set; } = 1;     // Để biết file có thay đổi không

        public string SyncedAt { get; set; } // ISO datetime
    }
}
