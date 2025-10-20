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
                case "Manager":
                    return RedirectToAction("Dashboard", "Coordinator");
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