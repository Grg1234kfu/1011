using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarConnect.Models
{
    public class Quote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RequestId { get; set; }

        [ForeignKey("RequestId")]
        public Request Request { get; set; }

        [Required]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public Vendor Vendor { get; set; }

        // System Details
        [Column(TypeName = "decimal(18,2)")]
        public decimal SystemSize { get; set; } // kW

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; } // USD

        [Column(TypeName = "decimal(18,2)")]
        public decimal? InstallationCost { get; set; } // USD

        [StringLength(200)]
        public string PanelBrand { get; set; }

        [StringLength(200)]
        public string InverterBrand { get; set; }

        [StringLength(200)]
        public string BatteryBrand { get; set; }

        [StringLength(2000)]
        public string SystemDescription { get; set; }

        [Range(1, 365)]
        public int EstimatedInstallationDays { get; set; }

        [Range(1, 50)]
        public int WarrantyYears { get; set; }

        // Status
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        [StringLength(1000)]
        public string? ClientResponse { get; set; }  // Added ?
    }
}