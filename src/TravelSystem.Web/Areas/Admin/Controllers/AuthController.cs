using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TravelSystem.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly AppDbContext _db;

        public AuthController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // Nếu đã login Admin thì chuyển hướng về Dashboard
            if (User.Identity != null && User.Identity.IsAuthenticated && User.HasClaim(ClaimTypes.Role, "Admin"))
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == password && u.Role == 0);

            if (user == null || !user.IsActive)
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, "AdminAuth");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("AdminAuth", principal);

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminAuth");
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content("Bạn không có quyền truy cập trang này (Cần quyền Admin).");
        }
    }
}
