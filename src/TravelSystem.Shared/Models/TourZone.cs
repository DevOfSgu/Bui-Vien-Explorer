using System;

namespace TravelSystem.Shared.Models
{
    public class TourZone
    {
        public int TourId { get; set; }
        public int ZoneId { get; set; }
        public int OrderIndex { get; set; }

        // Navigation properties
        public Tour? Tour { get; set; }
        public Zone? Zone { get; set; }
    }
}
