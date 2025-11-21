//--------------------------Start Of File--------------------------//
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public enum UserRole
    {
        Lecturer,
        Coordinator,
        Manager,
        HR
    }

    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public UserRole UserRole { get; set; }

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(100)]
        public string? Faculty { get; set; }

        [StringLength(100)]
        public string? Campus { get; set; }

        // HR-set hourly rate for lecturers
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? LastModified { get; set; }

        // Navigation property
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();

        // Computed property for full name
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        // Computed properties for compatibility with existing code
        [NotMapped]
        public string Role
        {
            get => UserRole.ToString();
            set
            {
                if (Enum.TryParse<UserRole>(value, true, out var role))
                {
                    UserRole = role;
                }
            }
        }

        [NotMapped]
        public string? Phone
        {
            get => PhoneNumber;
            set => PhoneNumber = value;
        }

        [NotMapped]
        public DateTime CreatedAt
        {
            get => CreatedDate;
            set => CreatedDate = value;
        }

        [NotMapped]
        public DateTime? UpdatedAt
        {
            get => LastModified;
            set => LastModified = value;
        }
    }
}
//--------------------------End Of File--------------------------//