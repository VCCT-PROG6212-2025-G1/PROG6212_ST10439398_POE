//--------------------------Start Of File--------------------------//
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public enum ClaimStatus
    {
        Draft = 0,
        Submitted = 1,
        UnderReview = 2,
        Approved = 3,
        Rejected = 4,
        PaymentProcessing = 5,
        Paid = 6
    }

    public class Claim
    {
        [Key]
        public int ClaimId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int ModuleId { get; set; }

        [Required]
        [Range(0, 180)]
        public decimal HoursWorked { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public string ClaimPeriod { get; set; }

        public string AdditionalNotes { get; set; } = string.Empty;

        [Required]
        public ClaimStatus CurrentStatus { get; set; } = ClaimStatus.Draft;

        public DateTime SubmissionDate { get; set; }

        public DateTime? LastModified { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [ForeignKey("ModuleId")]
        public virtual Module Module { get; set; }

        public virtual ICollection<SupportingDocument> SupportingDocuments { get; set; }
        public virtual ICollection<ClaimStatusHistory> StatusHistory { get; set; }
    }
}
//--------------------------End Of File--------------------------//