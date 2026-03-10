using System.ComponentModel.DataAnnotations;

namespace TravelSystem.Web.Areas.Admin.Models
{
    public class AdminSettingsViewModel
    {
        [Display(Name = "Full name")]
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string? Password { get; set; }

        [Display(Name = "Default language")]
        public string DefaultLanguage { get; set; } = "vi";

        [Display(Name = "Enable API Sync")]
        public bool EnableApiSync { get; set; }
    }
}