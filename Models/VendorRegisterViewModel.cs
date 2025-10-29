using System.ComponentModel.DataAnnotations;

namespace SolarConnect.Models.ViewModels
{
    public class VendorRegisterViewModel
    {
        // Personal Information
        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        // Company Information
        [Required(ErrorMessage = "Company name is required")]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; }

        [Required(ErrorMessage = "Business license is required")]
        [Display(Name = "Business License Number")]
        public string BusinessLicense { get; set; }

        [Required(ErrorMessage = "Years of experience is required")]
        [Range(0, 100, ErrorMessage = "Years must be between 0 and 100")]
        [Display(Name = "Years of Experience")]
        public int YearsOfExperience { get; set; }

        [Required(ErrorMessage = "Service area is required")]
        [Display(Name = "Service Areas")]
        public string ServiceArea { get; set; }

        [Display(Name = "Specialization")]
        public string Specialization { get; set; }

        [Display(Name = "Website")]
        [Url(ErrorMessage = "Invalid URL")]
        public string Website { get; set; }

        [Display(Name = "Company Description")]
        public string CompanyDescription { get; set; }

        // Security
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }
    }
}