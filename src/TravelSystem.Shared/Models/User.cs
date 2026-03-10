namespace TravelSystem.Shared.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // new profile fields
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public int Role { get; set; } // 0 = Admin, 1 = Vendor
        public int? ShopId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Shop? Shop { get; set; }
    }
}
