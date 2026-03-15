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


        // Navigation
        public Shop? Shop { get; set; }
    }
}
