//--------------------------Start Of File--------------------------//
using System.ComponentModel.DataAnnotations;

namespace CMCS.ViewModels
{
    public class ClaimSubmissionViewModel
    {
        [Required(ErrorMessage = "Module is required")]
        public int ModuleId { get; set; }

        [Required(ErrorMessage = "Hours worked is required")]
        [Range(0.5, 180, ErrorMessage = "Hours must be between 0.5 and 180")]
        public decimal HoursWorked { get; set; }

        [Required(ErrorMessage = "Claim period is required")]
        public string ClaimPeriod { get; set; }
      
        public string? AdditionalNotes { get; set; }

        public List<IFormFile>? SupportingDocuments { get; set; }
    }
}
//--------------------------End Of File--------------------------//