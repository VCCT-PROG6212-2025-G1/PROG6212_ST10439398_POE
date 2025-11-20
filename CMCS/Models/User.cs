//--------------------------Start Of File--------------------------//
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = string.Empty; // Lecturer, Coordinator, Manager, HR

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(50)]
        public string? Department { get; set; }

        [StringLength(50)]
        public string? Faculty { get; set; }

        [StringLength(50)]
        public string? Campus { get; set; }

        // HR-set hourly rate for lecturers
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();

        // Computed property for full name
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}
//--------------------------End Of File--------------------------//