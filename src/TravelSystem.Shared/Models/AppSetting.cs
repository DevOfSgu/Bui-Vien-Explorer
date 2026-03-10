using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace TravelSystem.Shared.Models
{
    public class AppSetting
    {
        [PrimaryKey]
        public string Key { get; set; } // Ví dụ: "Language", "AutoPlay"
        public string Value { get; set; } // Ví dụ: "vi", "1"
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
