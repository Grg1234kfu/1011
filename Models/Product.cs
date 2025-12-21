using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarConnect.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public Vendor Vendor { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100)]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [StringLength(50)]
        public string Category { get; set; }

        [Required(ErrorMessage = "Brand is required")]
        [StringLength(100)]
        public string Brand { get; set; }

        [StringLength(100)]
        public string? Model { get; set; }  // ⭐ NULLABLE

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [StringLength(50)]
        public string? PowerOutput { get; set; }  // ⭐ NULLABLE

        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000)]
        public string Description { get; set; }

        [StringLength(1000)]
        public string? Specifications { get; set; }  // ⭐ NULLABLE

        [Range(0, 100)]
        public int WarrantyYears { get; set; } = 0;

        public bool IsAvailable { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; } = 0;

        [StringLength(500)]
        public string? ImageUrl { get; set; }  // ⭐ NULLABLE

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}