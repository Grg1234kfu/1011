using System.ComponentModel.DataAnnotations;

namespace SolarConnect.Models.ViewModels
{
    public class CreateRequestViewModel
    {
        [Required(ErrorMessage = "Property address is required")]
        [Display(Name = "Property Address")]
        public string PropertyAddress { get; set; }

        [Required(ErrorMessage = "Property type is required")]
        [Display(Name = "Property Type")]
        public string PropertyType { get; set; }

        [Required(ErrorMessage = "Roof area is required")]
        [Range(1, 10000, ErrorMessage = "Roof area must be between 1 and 10000 m²")]
        [Display(Name = "Roof Area (m²)")]
        public decimal RoofArea { get; set; }

        [Required(ErrorMessage = "Monthly consumption is required")]
        [Range(1, 100000, ErrorMessage = "Monthly consumption must be between 1 and 100000 kWh")]
        [Display(Name = "Monthly Consumption (kWh)")]
        public decimal MonthlyConsumption { get; set; }

        [Range(0, 100000, ErrorMessage = "Bill amount must be between 0 and 100000 USD")]
        [Display(Name = "Monthly Bill (USD)")]
        public decimal? MonthlyBill { get; set; }

        [Display(Name = "Additional Notes")]
        public string AdditionalNotes { get; set; }
    }
}