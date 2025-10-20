using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using CMCS.Models;
using CMCS.Data;
using System.Linq;

namespace CMCS.Controllers
{
    public class AccountController : Controller
    {
        private readonly CMCSContext _context;

        public AccountController(CMCSContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string role)
        {
            // Simple authentication - in production, use proper password hashing
            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.IsActive);

            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.FirstName + " " + user.LastName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Role, user.UserRole.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                // Redirect based on role
                switch (user.UserRole)
                {
                    case UserRole.Lecturer:
                        return RedirectToAction("Dashboard", "Lecturer");
                    case UserRole.Coordinator:
                    case UserRole.Manager:
                        return RedirectToAction("Dashboard", "Coordinator");
                    default:
                        return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.ErrorMessage = "Invalid login credentials";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}