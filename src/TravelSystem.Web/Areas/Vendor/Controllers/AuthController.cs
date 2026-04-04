using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TravelSystem.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace TravelSystem.Web.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    public class AuthController : Controller
    {
        private readonly AppDbContext _db;
        private readonly TravelSystem.Web.Services.INotificationService _notificationService;

        public AuthController(AppDbContext db, TravelSystem.Web.Services.INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // Nếu đã login Vendor thì chuyển hướng về Dashboard
            if (User.Identity != null && User.Identity.IsAuthenticated && User.HasClaim(ClaimTypes.Role, "Vendor"))
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Role == 1);

            if (user == null || !user.IsActive || !Helpers.PasswordHelper.VerifyPassword(user, password, out var needsUpgrade))
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.";
                return View();
            }

            if (needsUpgrade)
            {
                user.PasswordHash = Helpers.PasswordHelper.HashPassword(user, password);
                await _db.SaveChangesAsync();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, "Vendor")
            };

            if (user.ShopId.HasValue)
            {
                claims.Add(new Claim("ShopId", user.ShopId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "VendorAuth");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("VendorAuth", principal);

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.HasClaim(ClaimTypes.Role, "Vendor"))
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string storeName, string ownerName, string username, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View();
            }

            var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (existingUser != null)
            {
                ViewBag.Error = "Email address is already in use.";
                return View();
            }

            var newShop = new TravelSystem.Shared.Models.Shop
            {
                Name = storeName,
                Address = "To be updated",
                PhoneNumber = "To be updated",
                ImageUrl = "/img/default-shop.png"
            };

            _db.Shops.Add(newShop);
            await _db.SaveChangesAsync();

            var newUser = new TravelSystem.Shared.Models.User
            {
                Username = username,
                PasswordHash = string.Empty,
                Role = 1,
                ShopId = newShop.Id,
                IsActive = false
            };
            newUser.PasswordHash = Helpers.PasswordHelper.HashPassword(newUser, password);

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            await _notificationService.NotifyAdminsAsync(
                $"Vendor mới đăng ký: {storeName} ({username})",
                Url.Action("Index", "Users", new { area = "Admin" }));

            ViewBag.Success = "Registration successful! Your store is pending admin approval.";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("VendorAuth");
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content("Bạn không có quyền truy cập trang này (Cần quyền Vendor).");
        }
    }
}
