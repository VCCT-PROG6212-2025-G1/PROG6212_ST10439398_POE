using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class Module
    {
        [Key]
        public int ModuleId { get; set; }

        [Required(ErrorMessage = "Module code is required")]
        [StringLength(20)]
        [Display(Name = "Module Code")]
        public string ModuleCode { get; set; }

        [Required(ErrorMessage = "Module name is required")]
        [StringLength(200)]
        [Display(Name = "Module Name")]
        public string ModuleName { get; set; }

        [Required(ErrorMessage = "Hourly rate is required")]
        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, 9999.99, ErrorMessage = "Hourly rate must be between R0.01 and R9999.99")]
        [Display(Name = "Standard Hourly Rate")]
        public decimal StandardHourlyRate { get; set; }

        [Display(Name = "Description")]
        [StringLength(500)]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Modified")]
        public DateTime? LastModified { get; set; }

        // Navigation properties
        public virtual ICollection<User> Lecturers { get; set; }
        public virtual ICollection<Claim> Claims { get; set; }
    }
}