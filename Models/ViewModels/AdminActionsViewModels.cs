using System;
using System.Collections.Generic;

namespace SolarConnect.Models.ViewModels
{
    public class AdminActionsFilterViewModel
    {
        public string? SearchEmail { get; set; }
        public bool OnlyNoQuotes { get; set; }
        public int? ClientId { get; set; }
        public int? VendorId { get; set; }
    }

    public class AdminQuoteRowViewModel
    {
        public int QuoteId { get; set; }
        public int VendorId { get; set; }
        public string VendorEmail { get; set; } = "";
        public string VendorName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = "";
        public DateTime SubmittedAt { get; set; }
    }

    public class AdminRequestActionsRowViewModel
    {
        public int RequestId { get; set; }
        public int ClientId { get; set; }
        public string ClientEmail { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string Status { get; set; } = "";
        public string PropertyAddress { get; set; } = "";
        public string PropertyType { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int QuotesCount { get; set; }
        public List<AdminQuoteRowViewModel> Quotes { get; set; } = new();
    }

    public class AdminActionsPageViewModel
    {
        public AdminActionsFilterViewModel Filter { get; set; } = new();
        public List<AdminRequestActionsRowViewModel> Rows { get; set; } = new();

        public List<(int Id, string Email, string Name)> Clients { get; set; } = new();
        public List<(int Id, string Email, string Name)> Vendors { get; set; } = new();
    }
}
