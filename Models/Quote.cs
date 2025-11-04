using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SolarConnect.Models
{
    public class Quote
    {
        [Key]
        public int Id { get; set; }

        // === Foreign Keys ===
        [Required]
        public int RequestId { get; set; }

        [ForeignKey("RequestId")]
        [ValidateNever] // ✅ Prevent MVC from validating this navigation property
        public Request Request { get; set; }

        [Required]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        [ValidateNever] // ✅ Prevent MVC from validating this navigation property
        public Vendor Vendor { get; set; }

        // === Quote Information ===
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.1, 1000000, ErrorMessage = "Total price must be greater than 0.")]
        public decimal TotalPrice { get; set; } // USD

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.1, 10000, ErrorMessage = "System size must be greater than 0.")]
        public decimal? SystemSize { get; set; } // kW

        [Range(0, 50, ErrorMessage = "Warranty must be between 0 and 50 years.")]
        public int? WarrantyYears { get; set; }

        [StringLength(1000)]
        public string Details { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
