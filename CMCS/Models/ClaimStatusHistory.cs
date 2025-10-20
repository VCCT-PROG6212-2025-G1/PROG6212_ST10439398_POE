using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class ClaimStatusHistory
    {
        [Key]
        public int StatusId { get; set; }

        [Required]
        public int ClaimId { get; set; }

        public int? ChangedBy { get; set; }

        [Required]
        public ClaimStatus PreviousStatus { get; set; }

        [Required]
        public ClaimStatus NewStatus { get; set; }

        public DateTime ChangeDate { get; set; } = DateTime.Now;

        public string Comments { get; set; }

        // Navigation properties
        [ForeignKey("ClaimId")]
        public virtual Claim Claim { get; set; }

        [ForeignKey("ChangedBy")]
        public virtual User User { get; set; }
    }
}