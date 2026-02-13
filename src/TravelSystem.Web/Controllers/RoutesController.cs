using Microsoft.AspNetCore.Mvc;

namespace TravelSystem.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoutesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new { message = "API works!", timestamp = DateTime.Now });
    }
}