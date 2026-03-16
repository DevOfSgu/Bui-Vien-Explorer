using System;

namespace TravelSystem.Shared.Models
{
    public class Analytics
    {
        public int Id { get; set; }
        public int? ZoneId { get; set; }

        public Guid SessionId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public int DwellTimeSeconds { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
