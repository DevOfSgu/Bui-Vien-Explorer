using System.ComponentModel.DataAnnotations;

namespace TravelSystem.Web.Areas.Admin.Models
{
    public class AdminSettingsViewModel
    {
        [Display(Name = "Họ và tên")]
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string? Password { get; set; }
    }
}
