//--------------------------Start Of File--------------------------//
using System.ComponentModel.DataAnnotations;

namespace CMCS.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a role")]
        public string UserType { get; set; } = "Lecturer";

        // ✅ Added: Remember me functionality
        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; } = false;
    }
}
//--------------------------End Of File--------------------------//