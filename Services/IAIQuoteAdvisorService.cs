using SolarConnect.Models;

namespace SolarConnect.Services
{
    public interface IAIQuoteAdvisorService
    {
        Task<string> AnalyzeQuotesAsync(List<Quote> quotes, Request request);
    }
}