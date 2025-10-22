//--------------------------Start Of File--------------------------//
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Phone]
        public string PhoneNumber { get; set; }

        [Required]
        public UserRole UserRole { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Claim> Claims { get; set; }
        public virtual ICollection<Module> Modules { get; set; }
    }
}
//--------------------------End Of File--------------------------//