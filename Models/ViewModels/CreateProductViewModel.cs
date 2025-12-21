using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SolarConnect.Models.ViewModels
{
    public class CreateProductViewModel
    {
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100)]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "Category is required")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Brand is required")]
        [StringLength(100)]
        public string Brand { get; set; }

        [StringLength(100)]
        public string? Model { get; set; }  // ⭐ OPTIONAL

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [StringLength(50)]
        public string? PowerOutput { get; set; }  // ⭐ OPTIONAL

        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000)]
        public string Description { get; set; }

        [StringLength(1000)]
        public string? Specifications { get; set; }  // ⭐ OPTIONAL

        [Range(0, 100)]
        public int WarrantyYears { get; set; } = 0;

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; } = 0;

        public IFormFile? ProductImage { get; set; }  // ⭐ OPTIONAL

        public string? ImageUrl { get; set; }  // ⭐ OPTIONAL
    }
}