// ViewModels/ClaimSubmissionViewModel.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CMCS.ViewModels
{
    public class ClaimSubmissionViewModel
    {
        [Required]
        [Display(Name = "Module/Course")]
        public int ModuleId { get; set; }

        [Required]
        [Display(Name = "Claim Period")]
        public string ClaimPeriod { get; set; }

        [Required]
        [Range(0, 999.99)]
        [Display(Name = "Hours Worked")]
        public decimal HoursWorked { get; set; }

        [Display(Name = "Additional Notes")]
        public string AdditionalNotes { get; set; }

        [Display(Name = "Supporting Documents")]
        public List<IFormFile> Documents { get; set; }

        public SelectList AvailableModules { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}