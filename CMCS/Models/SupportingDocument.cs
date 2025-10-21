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
        [Display(Name = "File Name")]
        public string FileName { get; set; }

        [Required]
        [Display(Name = "File Path")]
        public string FilePath { get; set; }

        [Display(Name = "File Size (bytes)")]
        public long FileSize { get; set; }

        [StringLength(50)]
        [Display(Name = "File Type")]
        public string FileType { get; set; }

        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        [Display(Name = "Description")]
        [StringLength(500)]
        public string Description { get; set; }

        // Navigation property
        [ForeignKey("ClaimId")]
        public virtual Claim Claim { get; set; }
    }
}