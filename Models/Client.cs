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

        // Property Information (ALL NULLABLE - filled when creating requests)
        [StringLength(500)]
        public string? Address { get; set; }  // Added ?

        [StringLength(100)]
        public string? PropertyType { get; set; }  // Added ?

        [Column(TypeName = "decimal(18,2)")]
        public decimal? RoofArea { get; set; }

        // Energy Consumption (NULLABLE)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MonthlyConsumption { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MonthlyElectricityBill { get; set; }

        [StringLength(1000)]
        public string? AdditionalNotes { get; set; }  // Added ?

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}