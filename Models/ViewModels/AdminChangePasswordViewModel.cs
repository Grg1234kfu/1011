using System.ComponentModel.DataAnnotations;

namespace SolarConnect.Models.ViewModels
{
    public class AdminChangePasswordViewModel
    {
        public int UserId { get; set; }

        public string Email { get; set; } = "";

        [Required, MinLength(6)]
        public string NewPassword { get; set; } = "";

        [Required, Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = "";
    }
}
