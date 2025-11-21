//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CMCS.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _allowedRoles;

        public SessionAuthorizeAttribute(params string[] roles)
        {
            _allowedRoles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Don't redirect if already on login or access denied pages
            var currentPath = context.HttpContext.Request.Path.Value?.ToLower();
            if (currentPath == "/account/login" || currentPath == "/account/accessdenied")
            {
                return;
            }

            // Check if user is logged in via session
            var userId = context.HttpContext.Session.GetInt32("UserId");
            var userRole = context.HttpContext.Session.GetString("UserRole");

            if (userId == null || string.IsNullOrEmpty(userRole))
            {
                // Not logged in - redirect to login with return URL
                context.Result = new RedirectToActionResult(
                    "Login", 
                    "Account", 
                    new { returnUrl = context.HttpContext.Request.Path });
                return;
            }

            // Check if user has required role
            if (_allowedRoles.Length > 0 && !_allowedRoles.Contains(userRole))
            {
                // User doesn't have required role - redirect to access denied
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }
        }
    }
}
//--------------------------End Of File--------------------------//