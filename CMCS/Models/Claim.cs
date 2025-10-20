using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public enum ClaimStatus
    {
        Draft,
        Submitted,
        UnderReview,
        Approved,
        Rejected,
        Paid
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
        [Range(0, 999.99)]
        public decimal HoursWorked { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public string ClaimPeriod { get; set; }

        public string AdditionalNotes { get; set; }

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