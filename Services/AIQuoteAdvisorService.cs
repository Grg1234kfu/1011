using System.Text;
using SolarConnect.Models;

namespace SolarConnect.Services
{
    public class AIQuoteAdvisorService : IAIQuoteAdvisorService
    {
        public async Task<string> AnalyzeQuotesAsync(List<Quote> quotes, Request request)
        {
            return await Task.Run(() => AnalyzeQuotes(quotes, request));
        }

        private string AnalyzeQuotes(List<Quote> quotes, Request request)
        {
            if (quotes == null || quotes.Count == 0)
            {
                return "No quotes available to analyze.";
            }

            if (quotes.Count == 1)
            {
                var singleQuote = quotes[0];
                return $@"## 📊 Single Quote Analysis

You currently have only **one quote** from **{singleQuote.Vendor.CompanyName}**.

### Quote Details:
- **Total Price:** ${singleQuote.TotalPrice:N2}
- **System Size:** {singleQuote.SystemSize} kW
- **Price per kW:** ${(singleQuote.TotalPrice / singleQuote.SystemSize):N2}
- **Equipment:** {singleQuote.PanelBrand} panels, {singleQuote.InverterBrand} inverter
- **Installation:** {singleQuote.EstimatedInstallationDays} days
- **Warranty:** {singleQuote.WarrantyYears} years
- **Vendor Rating:** {singleQuote.Vendor.Rating:F1}/5.0 ⭐

### 💡 Recommendation:
I recommend **waiting for at least 2-3 more quotes** before making a decision. This allows you to:
- Compare pricing and see if you're getting fair market value
- Evaluate different equipment options
- Understand the range of warranties offered
- Negotiate better terms with multiple options

**Getting multiple quotes is standard practice** and vendors expect it. This single quote gives you a baseline, but more competition typically results in better offers!";
            }

            // Calculate metrics for all quotes
            var analysis = new StringBuilder();
            var sortedByPrice = quotes.OrderBy(q => q.TotalPrice).ToList();
            var sortedByPricePerKW = quotes.OrderBy(q => q.TotalPrice / q.SystemSize).ToList();
            var sortedByWarranty = quotes.OrderByDescending(q => q.WarrantyYears).ToList();
            var sortedByTimeline = quotes.OrderBy(q => q.EstimatedInstallationDays).ToList();
            var sortedByRating = quotes.OrderByDescending(q => q.Vendor.Rating).ToList();

            var avgPrice = quotes.Average(q => q.TotalPrice);
            var avgPricePerKW = quotes.Average(q => q.TotalPrice / q.SystemSize);
            var avgWarranty = quotes.Average(q => q.WarrantyYears);

            // Determine best quote based on scoring
            var bestQuote = DetermineBestQuote(quotes);

            analysis.AppendLine("## 🏆 Recommended Quote");
            analysis.AppendLine();
            analysis.AppendLine($"### **{bestQuote.Vendor.CompanyName}**");
            analysis.AppendLine();
            analysis.AppendLine($"**Price:** ${bestQuote.TotalPrice:N2} | **System:** {bestQuote.SystemSize} kW | **Rating:** {bestQuote.Vendor.Rating:F1}/5 ⭐");
            analysis.AppendLine();
            analysis.AppendLine("### Why This Quote Stands Out:");
            analysis.AppendLine();

            // Generate reasoning
            var reasons = GenerateReasons(bestQuote, quotes, avgPrice, avgPricePerKW, (decimal)avgWarranty);
            foreach (var reason in reasons)
            {
                analysis.AppendLine($"✅ **{reason}**");
                analysis.AppendLine();
            }

            analysis.AppendLine("---");
            analysis.AppendLine();
            analysis.AppendLine("## 📊 Detailed Comparison");
            analysis.AppendLine();

            // Compare all quotes
            foreach (var quote in sortedByPrice)
            {
                analysis.AppendLine($"### {quote.Vendor.CompanyName}");
                analysis.AppendLine();

                var pricePerKW = quote.TotalPrice / quote.SystemSize;
                var priceStatus = pricePerKW < avgPricePerKW ? "Below Average ✅" : pricePerKW > avgPricePerKW ? "Above Average ⚠️" : "Average";

                analysis.AppendLine($"**💰 Pricing:**");
                analysis.AppendLine($"- Total: ${quote.TotalPrice:N2}");
                analysis.AppendLine($"- Per kW: ${pricePerKW:N2} ({priceStatus})");
                analysis.AppendLine();

                analysis.AppendLine($"**⚡ System:**");
                analysis.AppendLine($"- Size: {quote.SystemSize} kW");
                analysis.AppendLine($"- Panels: {quote.PanelBrand}");
                analysis.AppendLine($"- Inverter: {quote.InverterBrand}");
                if (!string.IsNullOrEmpty(quote.BatteryBrand))
                {
                    analysis.AppendLine($"- Battery: {quote.BatteryBrand}");
                }
                analysis.AppendLine();

                analysis.AppendLine($"**📅 Timeline & Warranty:**");
                analysis.AppendLine($"- Installation: {quote.EstimatedInstallationDays} days");
                analysis.AppendLine($"- Warranty: {quote.WarrantyYears} years");
                analysis.AppendLine();

                analysis.AppendLine($"**⭐ Vendor Rating:** {quote.Vendor.Rating:F1}/5.0");
                analysis.AppendLine();

                // Pros and Cons
                var proscons = GenerateProsAndCons(quote, sortedByPrice, sortedByWarranty, sortedByTimeline, sortedByRating);
                if (proscons.Pros.Any())
                {
                    analysis.AppendLine("**Strengths:**");
                    foreach (var pro in proscons.Pros)
                    {
                        analysis.AppendLine($"- {pro}");
                    }
                    analysis.AppendLine();
                }

                if (proscons.Cons.Any())
                {
                    analysis.AppendLine("**Considerations:**");
                    foreach (var con in proscons.Cons)
                    {
                        analysis.AppendLine($"- {con}");
                    }
                    analysis.AppendLine();
                }

                analysis.AppendLine("---");
                analysis.AppendLine();
            }

            analysis.AppendLine("## 💡 Final Recommendation");
            analysis.AppendLine();
            analysis.AppendLine($"Based on comprehensive analysis of **{quotes.Count} quotes**, I recommend **{bestQuote.Vendor.CompanyName}** as the best overall value for your {request.PropertyType} solar installation.");
            analysis.AppendLine();
            analysis.AppendLine("### Key Factors Considered:");
            analysis.AppendLine("- Price competitiveness (30%)");
            analysis.AppendLine("- Equipment quality (25%)");
            analysis.AppendLine("- Warranty coverage (20%)");
            analysis.AppendLine("- Vendor reputation (15%)");
            analysis.AppendLine("- Installation timeline (10%)");
            analysis.AppendLine();
            analysis.AppendLine("### Next Steps:");
            analysis.AppendLine("1. Review the detailed comparison above");
            analysis.AppendLine("2. Contact the recommended vendor for any clarifications");
            analysis.AppendLine("3. Verify equipment specifications and warranty terms");
            analysis.AppendLine("4. Schedule a site visit if needed");
            analysis.AppendLine("5. Accept the quote through your SolarConnect dashboard");

            return analysis.ToString();
        }

        private Quote DetermineBestQuote(List<Quote> quotes)
        {
            var scores = new Dictionary<Quote, decimal>();

            foreach (var quote in quotes)
            {
                decimal score = 0;

                // Price competitiveness (30 points) - lower is better
                var pricePerKW = quote.TotalPrice / quote.SystemSize;
                var minPricePerKW = quotes.Min(q => q.TotalPrice / q.SystemSize);
                var maxPricePerKW = quotes.Max(q => q.TotalPrice / q.SystemSize);
                var priceScore = maxPricePerKW == minPricePerKW ? 30 : 30 * (1 - (pricePerKW - minPricePerKW) / (maxPricePerKW - minPricePerKW));
                score += priceScore;

                // Equipment quality (25 points) - based on known brands
                var equipmentScore = EvaluateEquipment(quote.PanelBrand, quote.InverterBrand);
                score += equipmentScore;

                // Warranty (20 points)
                var maxWarranty = quotes.Max(q => q.WarrantyYears);
                var warrantyScore = maxWarranty == 0 ? 20 : 20 * ((decimal)quote.WarrantyYears / maxWarranty);
                score += warrantyScore;

                // Vendor rating (15 points)
                score += (decimal)(quote.Vendor.Rating * 3);

                // Installation timeline (10 points) - faster is better
                var minDays = quotes.Min(q => q.EstimatedInstallationDays);
                var maxDays = quotes.Max(q => q.EstimatedInstallationDays);
                var timelineScore = maxDays == minDays ? 10 : 10 * (1 - (quote.EstimatedInstallationDays - minDays) / (decimal)(maxDays - minDays));
                score += timelineScore;

                scores[quote] = score;
            }

            return scores.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        private decimal EvaluateEquipment(string panelBrand, string inverterBrand)
        {
            var premiumPanels = new[] { "SunPower", "LG", "Panasonic", "REC", "Q CELLS" };
            var goodPanels = new[] { "Canadian Solar", "JinkoSolar", "Trina", "LONGi", "JA Solar" };

            var premiumInverters = new[] { "SMA", "Fronius", "SolarEdge", "Enphase" };
            var goodInverters = new[] { "Huawei", "Sungrow", "GoodWe", "Growatt" };

            decimal score = 0;

            // Panel score (15 points)
            if (premiumPanels.Any(p => panelBrand.Contains(p, StringComparison.OrdinalIgnoreCase)))
                score += 15;
            else if (goodPanels.Any(p => panelBrand.Contains(p, StringComparison.OrdinalIgnoreCase)))
                score += 12;
            else
                score += 8;

            // Inverter score (10 points)
            if (premiumInverters.Any(i => inverterBrand.Contains(i, StringComparison.OrdinalIgnoreCase)))
                score += 10;
            else if (goodInverters.Any(i => inverterBrand.Contains(i, StringComparison.OrdinalIgnoreCase)))
                score += 8;
            else
                score += 5;

            return score;
        }

        private List<string> GenerateReasons(Quote bestQuote, List<Quote> allQuotes, decimal avgPrice, decimal avgPricePerKW, decimal avgWarranty)
        {
            var reasons = new List<string>();
            var pricePerKW = bestQuote.TotalPrice / bestQuote.SystemSize;

            if (bestQuote.TotalPrice == allQuotes.Min(q => q.TotalPrice))
                reasons.Add("Lowest total price among all quotes");
            else if (bestQuote.TotalPrice < avgPrice)
                reasons.Add($"Below average price (${(avgPrice - bestQuote.TotalPrice):N2} savings)");

            if (pricePerKW < avgPricePerKW)
                reasons.Add($"Competitive price per kW (${pricePerKW:N2} vs avg ${avgPricePerKW:N2})");

            if (bestQuote.WarrantyYears >= avgWarranty)
                reasons.Add($"Strong warranty coverage ({bestQuote.WarrantyYears} years)");

            if (bestQuote.Vendor.Rating >= 4.5m)
                reasons.Add($"Excellent vendor reputation ({bestQuote.Vendor.Rating:F1}/5 stars)");
            else if (bestQuote.Vendor.Rating >= 4.0m)
                reasons.Add($"Strong vendor reputation ({bestQuote.Vendor.Rating:F1}/5 stars)");

            if (bestQuote.EstimatedInstallationDays <= allQuotes.Min(q => q.EstimatedInstallationDays) + 5)
                reasons.Add($"Fast installation timeline ({bestQuote.EstimatedInstallationDays} days)");

            reasons.Add("Best overall value considering all factors");

            return reasons.Take(5).ToList();
        }

        private (List<string> Pros, List<string> Cons) GenerateProsAndCons(Quote quote, List<Quote> byPrice, List<Quote> byWarranty, List<Quote> byTimeline, List<Quote> byRating)
        {
            var pros = new List<string>();
            var cons = new List<string>();

            // Price
            if (byPrice.First() == quote)
                pros.Add("Lowest price");
            else if (byPrice.Last() == quote)
                cons.Add("Highest price");

            // Warranty
            if (byWarranty.First() == quote)
                pros.Add("Longest warranty");
            else if (byWarranty.Last() == quote)
                cons.Add("Shortest warranty");

            // Timeline
            if (byTimeline.First() == quote)
                pros.Add("Fastest installation");
            else if (byTimeline.Last() == quote)
                cons.Add("Longest installation time");

            // Rating
            if (quote.Vendor.Rating >= 4.5m)
                pros.Add("Top-rated vendor");
            else if (quote.Vendor.Rating < 3.5m)
                cons.Add("Lower vendor rating");

            return (pros, cons);
        }
    }
}