using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace TravelSystem.Shared.Models
{
    public class AppSetting
    {
        [PrimaryKey]
        public string Key { get; set; } = string.Empty; // Ví dụ: "Language", "AutoPlay"
        public string Value { get; set; } = string.Empty; // Ví dụ: "vi", "1"

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
