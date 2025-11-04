using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarConnect.Models
{
    public class Project
    {
        [Key]
        public int Id { get; set; }

        public int QuoteId { get; set; }
        [ForeignKey("QuoteId")]
        public Quote Quote { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";
        // Possible: Pending, In Progress, Completed, Cancelled

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? CompletionDate { get; set; }

        [StringLength(2000)]
        public string InstallationNotes { get; set; } // vendor updates

        [StringLength(500)]
        public string MaterialsUsed { get; set; } // products used

        [StringLength(500)]
        public string ProgressStage { get; set; } // e.g., "Panels Installed", "Wiring Complete"
    }
}
