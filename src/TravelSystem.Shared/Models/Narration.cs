namespace TravelSystem.Shared.Models
{
    /// <summary>Server-side model cho bảng Narrations (SQL Server)</summary>
    public class Narration
    {
        public int Id { get; set; }
        public int ZoneId { get; set; }
        public string Language { get; set; } = string.Empty;  // "vi", "en", "ja"...
        public string Text { get; set; } = string.Empty;       // Nội dung TTS
        public string? VoiceId { get; set; }                   // "vi-VN-Standard-A"

        // Approval Workflow: "Pending", "Approved", "Rejected"
        public string ApprovalStatus { get; set; } = "Pending";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? UpdatedBy { get; set; }
    }
}
