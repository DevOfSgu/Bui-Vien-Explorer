using System;

namespace TravelSystem.Shared.Models
{
    public class TourTranslation
    {
        public int Id { get; set; }
        public int TourId { get; set; }
        public string Language { get; set; } = "vi";
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Tour? Tour { get; set; }
    }
}
