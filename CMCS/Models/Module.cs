//--------------------------Start Of File--------------------------//
using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public class Module
    {
        public int ModuleId { get; set; }

        [Required]
        [StringLength(20)]
        public string ModuleCode { get; set; }

        [Required]
        [StringLength(200)]
        public string ModuleName { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(0.01, 10000)]
        public decimal StandardHourlyRate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? LastModified { get; set; }

        // Navigation properties
        public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    }
}
//--------------------------End Of File--------------------------//