using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
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
        [ValidateNever]
        public Client Client { get; set; }

        [Required, StringLength(500)]
        public string PropertyAddress { get; set; }

        [Required, StringLength(100)]
        public string PropertyType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RoofArea { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyConsumption { get; set; } // kWh

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MonthlyBill { get; set; } // USD

        [StringLength(1000)]
        public string AdditionalNotes { get; set; }

        [Required, StringLength(50)]
        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }
    }
}
