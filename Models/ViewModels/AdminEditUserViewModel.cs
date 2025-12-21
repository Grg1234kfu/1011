using System.ComponentModel.DataAnnotations;

namespace SolarConnect.Models.ViewModels
{
    public class AdminEditUserViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; } = "";

        [Required, StringLength(100)]
        public string LastName { get; set; } = "";

        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; } = "";

        [Required, StringLength(30)]
        public string PhoneNumber { get; set; } = "";

        [Required]
        public string Role { get; set; } = "Client";

        public bool IsActive { get; set; } = true;
    }
}
