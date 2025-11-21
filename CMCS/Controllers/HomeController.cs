//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CMCS.Models;

namespace CMCS.Controllers
{
    public class HomeController : Controller
    {
        // ? FIXED: Remove [Authorize] to prevent redirect loop
        // Let users access home page without auth
        public IActionResult Index()
        {
            // Check if user is authenticated
            if (User.Identity?.IsAuthenticated == true)
            {
                // Redirect authenticated users to their dashboard
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                return userRole switch
                {
                    "Lecturer" => RedirectToAction("Dashboard", "Lecturer"),
                    "Coordinator" => RedirectToAction("Dashboard", "Coordinator"),
                    "Manager" => RedirectToAction("Dashboard", "Manager"),
                    "HR" => RedirectToAction("Dashboard", "HR"),
                    _ => RedirectToAction("Login", "Account")
                };
            }

            // Show home page for unauthenticated users or redirect to login
            return RedirectToAction("Login", "Account");
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