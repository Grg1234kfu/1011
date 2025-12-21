namespace SolarConnect.Services
{
    public interface IEmailService
    {
        Task SendQuoteEmailAsync(string recipientEmail, string recipientName, byte[] quotePdf, string vendorCompany, decimal quotePrice);
    }
}
