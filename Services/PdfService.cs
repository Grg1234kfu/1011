using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SolarConnect.Models;

namespace SolarConnect.Services
{
    public class PdfService : IPdfService
    {
        public byte[] GenerateQuotePdf(Quote quote, Vendor vendor, Request request, Client client)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text("SOLAR QUOTE").FontSize(24).Bold().FontColor("#FF8C00");
                            column.Item().Text("SolarConnect Platform").FontSize(12).FontColor("#666666");
                        });
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(20);

                        // Quote Header
                        column.Item().Background("#FF8C00").Padding(15).Text(text =>
                        {
                            text.Span("Quote #" + quote.Id.ToString()).FontSize(18).Bold().FontColor("#FFFFFF");
                        });

                        // Vendor Info
                        column.Item().BorderBottom(2).BorderColor("#FF8C00").PaddingBottom(10)
                            .Text("Vendor Information").FontSize(16).Bold();

                        column.Item().Column(col =>
                        {
                            col.Item().Text("Company: " + vendor.CompanyName).Bold();
                            col.Item().Text("Contact: " + vendor.User.FirstName + " " + vendor.User.LastName);
                            col.Item().Text("Email: " + vendor.User.Email);
                            if (!string.IsNullOrEmpty(vendor.User.PhoneNumber))
                            {
                                col.Item().Text("Phone: " + vendor.User.PhoneNumber);
                            }
                            col.Item().Text("Rating: ⭐ " + vendor.Rating.ToString("F1") + "/5.0");
                        });

                        // Client Info
                        column.Item().PaddingTop(10).BorderBottom(2).BorderColor("#FF8C00").PaddingBottom(10)
                            .Text("Client Information").FontSize(16).Bold();

                        column.Item().Column(col =>
                        {
                            col.Item().Text("Name: " + client.User.FirstName + " " + client.User.LastName).Bold();
                            col.Item().Text("Email: " + client.User.Email);
                        });

                        // Request Info
                        column.Item().PaddingTop(10).BorderBottom(2).BorderColor("#FF8C00").PaddingBottom(10)
                            .Text("Request Details").FontSize(16).Bold();

                        column.Item().Column(col =>
                        {
                            col.Item().Text("Property Type: " + (request.PropertyType ?? "N/A"));
                            col.Item().Text("Property Address: " + (request.PropertyAddress ?? "N/A"));
                            col.Item().Text("Roof Area: " + request.RoofArea.ToString("F2") + " m²");
                            col.Item().Text("Monthly Consumption: " + request.MonthlyConsumption.ToString() + " kWh");
                            if (request.RecommendedSystemSize.HasValue)
                            {
                                col.Item().Text("Recommended System Size: " + request.RecommendedSystemSize.Value.ToString("F2") + " kW");
                            }
                        });

                        // Quote Details - Main Box
                        column.Item().PaddingTop(15).Background("#FFF9E6").Border(3).BorderColor("#FF8C00")
                            .Padding(20).Column(col =>
                            {
                                col.Item().Text("QUOTE DETAILS").FontSize(18).Bold().FontColor("#FF8C00");

                                col.Item().PaddingTop(15).Row(row =>
                                {
                                    row.RelativeItem().Text("Total Price:").FontSize(16).Bold();
                                    row.RelativeItem().AlignRight().Text("$" + quote.TotalPrice.ToString("N2"))
                                        .FontSize(24).Bold().FontColor("#28A745");
                                });

                                col.Item().PaddingTop(10).Text("Date: " + quote.SubmittedAt.ToString("MMMM dd, yyyy"));
                                col.Item().Text("Status: " + quote.Status.ToString()).FontColor("#1976D2").Bold();
                            });

                        // Terms
                        column.Item().PaddingTop(20).Column(col =>
                        {
                            col.Item().Text("Terms & Conditions").FontSize(12).Bold().FontColor("#666");
                            col.Item().PaddingTop(5).Text(text =>
                            {
                                text.Span("• Quote valid for 30 days\n").FontSize(9);
                                text.Span("• Final price subject to site inspection\n").FontSize(9);
                                text.Span("• All prices in USD\n").FontSize(9);
                            });
                        });

                        // Footer CTA
                        column.Item().PaddingTop(15).Background("#1976D2").Padding(15)
                            .Text("Log in to SolarConnect to accept this quote")
                            .FontSize(12).FontColor("#FFFFFF").Bold().AlignCenter();
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Generated by SolarConnect Platform - ");
                        text.Span(DateTime.Now.ToString("MMMM dd, yyyy")).Italic();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}