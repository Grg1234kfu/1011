using SolarConnect.Models;

namespace SolarConnect.Services
{
    public interface IPdfService
    {
        byte[] GenerateQuotePdf(Quote quote, Vendor vendor, Request request, Client client);
    }
}
