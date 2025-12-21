using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SolarConnect.Models;

namespace SolarConnect.Services
{
    public class GeminiAIService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GeminiAIService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
        }

        public async Task<string> AnalyzeQuotes(List<Quote> quotes, Request request)
        {
            if (quotes == null || quotes.Count == 0)
                return "No quotes available to analyze.";

            // Build the prompt for Gemini
            var prompt = BuildAnalysisPrompt(quotes, request);

            // Call Gemini API
            var result = await CallGeminiAPI(prompt);
            return result;
        }

        private string BuildAnalysisPrompt(List<Quote> quotes, Request request)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a solar energy expert helping a client choose the best solar installation quote.");
            sb.AppendLine($"\nClient's Request Details:");
            sb.AppendLine($"- Property Type: {request.PropertyType}");
            sb.AppendLine($"- Roof Area: {request.RoofArea} m²");
            sb.AppendLine($"- Monthly Consumption: {request.MonthlyConsumption} kWh");
            sb.AppendLine($"- Recommended System Size: {request.RecommendedSystemSize} kW");
            sb.AppendLine($"\nI have received {quotes.Count} quotes. Please analyze them and recommend the best option.");
            sb.AppendLine("\nQuotes:\n");

            for (int i = 0; i < quotes.Count; i++)
            {
                var quote = quotes[i];
                sb.AppendLine($"--- Quote {i + 1} from {quote.Vendor.CompanyName} ---");
                sb.AppendLine($"Total Price: ${quote.TotalPrice:N2}");
                sb.AppendLine($"System Size: {quote.SystemSize} kW");
                sb.AppendLine($"Installation Cost: ${quote.InstallationCost:N2}");
                sb.AppendLine($"Panel Brand: {quote.PanelBrand}");
                sb.AppendLine($"Inverter Brand: {quote.InverterBrand}");
                sb.AppendLine($"Battery: {(string.IsNullOrEmpty(quote.BatteryBrand) ? "Not included" : quote.BatteryBrand)}");
                sb.AppendLine($"Installation Time: {quote.EstimatedInstallationDays} days");
                sb.AppendLine($"Warranty: {quote.WarrantyYears} years");
                sb.AppendLine($"Description: {quote.SystemDescription}");
                sb.AppendLine($"Vendor Experience: {quote.Vendor.YearsOfExperience} years");
                sb.AppendLine($"Vendor Rating: {quote.Vendor.Rating}/5");
                sb.AppendLine();
            }

            sb.AppendLine("\nPlease provide:");
            sb.AppendLine("1. A clear comparison of all quotes");
            sb.AppendLine("2. Which quote offers the best VALUE (not just cheapest)");
            sb.AppendLine("3. Pros and cons of each option");
            sb.AppendLine("4. Your final recommendation with reasoning");
            sb.AppendLine("\nKeep your response concise, friendly, and easy to understand.");

            return sb.ToString();
        }

        private async Task<string> CallGeminiAPI(string prompt)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={_apiKey}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"Error calling AI: {response.StatusCode}";
                }

                // Parse the response
                var jsonDoc = JsonDocument.Parse(responseString);
                var text = jsonDoc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text;
            }
            catch (Exception ex)
            {
                return $"Error analyzing quotes: {ex.Message}";
            }
        }
    }
}