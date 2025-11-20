using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    public class HRController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
