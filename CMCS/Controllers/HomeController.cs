//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CMCS.Models;

namespace CMCS.Controllers
{
    public class HomeController : Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            // Redirect based on user role
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            switch (userRole)
            {
                case "Lecturer":
                    return RedirectToAction("Dashboard", "Lecturer");
                case "Coordinator":
                    return RedirectToAction("Dashboard", "Coordinator");
                case "Manager":
                    return RedirectToAction("Dashboard", "Manager");
                case "HR":
                    return RedirectToAction("Dashboard", "HR");
                default:
                    return View();
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
//--------------------------End Of File--------------------------//