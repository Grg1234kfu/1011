using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarConnect.Models
{
    public class Review
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int QuoteId { get; set; }

        [ForeignKey("QuoteId")]
        public Quote Quote { get; set; }

        [Required]
        public int ClientId { get; set; }

        [ForeignKey("ClientId")]
        public Client Client { get; set; }

        [Required]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public Vendor Vendor { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; } // 1-5 stars

        [StringLength(1000)]
        public string Comment { get; set; }

        [Range(1, 5)]
        public int QualityRating { get; set; } // Equipment quality

        [Range(1, 5)]
        public int ServiceRating { get; set; } // Customer service

        [Range(1, 5)]
        public int TimelinessRating { get; set; } // Installation timeline

        [Range(1, 5)]
        public int ValueRating { get; set; } // Value for money

        public bool WouldRecommend { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsVerified { get; set; } = true; // Only completed projects can review
    }
}