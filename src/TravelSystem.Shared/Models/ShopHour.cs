using System;

namespace TravelSystem.Shared.Models
{
    public class ShopHour
    {
        public int Id { get; set; }
        public int ShopId { get; set; }
        public int DayOfWeek { get; set; }
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }
        public bool IsOpen { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Shop? Shop { get; set; }
    }
}
