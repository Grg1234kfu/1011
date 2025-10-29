using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace SolarConnect.Models
{
    public class Vendor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        // Company Information
        [Required]
        [StringLength(200)]
        public string CompanyName { get; set; }

        [Required]
        [StringLength(100)]
        public string BusinessLicense { get; set; }

        [Range(0, 100)]
        public int YearsOfExperience { get; set; }

        [StringLength(500)]
        public string ServiceArea { get; set; } // e.g., "Beirut, Mount Lebanon, Tripoli"

        [StringLength(200)]
        public string Specialization { get; set; } // e.g., "Residential Solar, Commercial Installations"

        [StringLength(200)]
        public string Website { get; set; }

        [StringLength(1000)]
        public string CompanyDescription { get; set; }

        // Status
        public bool IsApproved { get; set; } = false; // Requires admin approval

        [Column(TypeName = "decimal(3,2)")]
        public decimal Rating { get; set; } = 0; // Average rating from reviews

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}