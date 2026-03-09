using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TravelSystem.Web.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(AuthenticationSchemes = "VendorAuth", Roles = "Vendor")]
    public class MenuController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
