using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarConnect.Models
{
    public class Client
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        // Property Information
        [StringLength(500)]
        public string Address { get; set; }

        [StringLength(100)]
        public string PropertyType { get; set; } // Residential, Commercial, Industrial, Agricultural

        [Column(TypeName = "decimal(18,2)")]
        public decimal? RoofArea { get; set; } // in m²

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MonthlyConsumption { get; set; } // in kWh

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MonthlyElectricityBill { get; set; } // in USD

        [StringLength(1000)]
        public string AdditionalNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}