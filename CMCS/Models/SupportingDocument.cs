//--------------------------Start Of File--------------------------//
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    public class SupportingDocument
    {
        [Key]
        public int DocumentId { get; set; }

        [Required]
        public int ClaimId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; }

        [Required]
        public long FileSize { get; set; }

        [Required]
        [StringLength(10)]
        public string FileType { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("ClaimId")]
        public virtual Claim Claim { get; set; }
    }
}
//--------------------------End Of File--------------------------//