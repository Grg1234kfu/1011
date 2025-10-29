using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarConnect.Models
{
    public class Request
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClientId { get; set; }

        [ForeignKey("ClientId")]
        public Client Client { get; set; }

        // Property Details
        [Required]
        [StringLength(500)]
        public string PropertyAddress { get; set; }

        [Required]
        [StringLength(100)]
        public string PropertyType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RoofArea { get; set; }

        // Energy Requirements
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyConsumption { get; set; } // kWh

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MonthlyBill { get; set; } // USD

        [Column(TypeName = "decimal(18,2)")]
        public decimal? RecommendedSystemSize { get; set; } // kW

        [StringLength(1000)]
        public string AdditionalNotes { get; set; }

        // Status
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Open"; // Open, Quoted, Accepted, Closed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ClosedAt { get; set; }
    }
}