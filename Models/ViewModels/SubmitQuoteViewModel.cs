using System.ComponentModel.DataAnnotations;

namespace SolarConnect.Models.ViewModels
{
    public class SubmitQuoteViewModel
    {
        [Required]
        public int RequestId { get; set; }

        [Required(ErrorMessage = "System size is required")]
        [Range(0.1, 1000, ErrorMessage = "System size must be between 0.1 and 1000 kW")]
        [Display(Name = "System Size (kW)")]
        public decimal SystemSize { get; set; }

        [Required(ErrorMessage = "Total price is required")]
        [Range(100, 1000000, ErrorMessage = "Price must be between $100 and $1,000,000")]
        [Display(Name = "Total Price (USD)")]
        public decimal TotalPrice { get; set; }

        [Range(0, 1000000, ErrorMessage = "Installation cost must be between $0 and $1,000,000")]
        [Display(Name = "Installation Cost (USD)")]
        public decimal? InstallationCost { get; set; }

        [Required(ErrorMessage = "Panel brand is required")]
        [StringLength(200)]
        [Display(Name = "Solar Panel Brand")]
        public string PanelBrand { get; set; }

        [Required(ErrorMessage = "Inverter brand is required")]
        [StringLength(200)]
        [Display(Name = "Inverter Brand")]
        public string InverterBrand { get; set; }

        [StringLength(200)]
        [Display(Name = "Battery Brand (Optional)")]
        public string BatteryBrand { get; set; }

        [Required(ErrorMessage = "System description is required")]
        [StringLength(2000)]
        [Display(Name = "System Description")]
        public string SystemDescription { get; set; }

        [Required(ErrorMessage = "Installation timeline is required")]
        [Range(1, 365, ErrorMessage = "Timeline must be between 1 and 365 days")]
        [Display(Name = "Estimated Installation Days")]
        public int EstimatedInstallationDays { get; set; }

        [Required(ErrorMessage = "Warranty period is required")]
        [Range(1, 50, ErrorMessage = "Warranty must be between 1 and 50 years")]
        [Display(Name = "Warranty (Years)")]
        public int WarrantyYears { get; set; }
    }
}