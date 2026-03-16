using SQLite;

namespace TravelSystem.Shared.Models
{
    public class LocalNarration
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string ZoneId { get; set; } = string.Empty; // Liên kết với điểm dừng


        public string Language { get; set; } = string.Empty; // "vi", "en"...
        public string Text { get; set; } = string.Empty;     // Script TTS (nội dung thuyết minh)
        public string VoiceId { get; set; } = string.Empty; // Tùy chọn giọng đọc TTS


        // Hỗ trợ phát file MP3 thực tế
        public string FileUrl { get; set; } = string.Empty;  // URL file MP3 trên server/CDN
        public string LocalFilePath { get; set; } = string.Empty; // Đường dẫn file đã tải về thiết bị

        public int Version { get; set; } = 1;     // Để biết file có thay đổi không

        public string SyncedAt { get; set; } = string.Empty; // ISO datetime

    }
}
