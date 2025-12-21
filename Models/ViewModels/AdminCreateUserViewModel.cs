using System.ComponentModel.DataAnnotations;

namespace SolarConnect.Models.ViewModels
{
    public class AdminCreateUserViewModel
    {
        [Required, StringLength(100)]
        public string FirstName { get; set; } = "";

        [Required, StringLength(100)]
        public string LastName { get; set; } = "";

        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; } = "";

        [Required, StringLength(30)]
        public string PhoneNumber { get; set; } = "";

        [Required]
        public string Role { get; set; } = "Client"; // Client | Vendor | Admin

        [Required, MinLength(6)]
        public string Password { get; set; } = "";

        // Optional client fields
        public string? Address { get; set; }
        public string? PropertyType { get; set; }
        public string? AdditionalNotes { get; set; }

        // Optional vendor fields
        public string? CompanyName { get; set; }
        public string? BusinessLicense { get; set; }
        public int? YearsOfExperience { get; set; }
        public string? ServiceArea { get; set; }
        public string? Specialization { get; set; }
        public string? Website { get; set; }
        public string? CompanyDescription { get; set; }

        // if Role=Vendor and you want admin to set approval
        public bool? IsApproved { get; set; }
    }
}
