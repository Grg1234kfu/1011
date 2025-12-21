using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SolarConnect.Models;

namespace SolarConnect.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendQuoteEmailAsync(string recipientEmail, string recipientName,
            byte[] quotePdf, string vendorCompany, decimal quotePrice)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                message.To.Add(new MailboxAddress(recipientName, recipientEmail));
                message.Subject = "New Solar Quote from " + vendorCompany + " - SolarConnect";

                var builder = new BodyBuilder();

                // HTML Email Body
                builder.HtmlBody = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: 'Arial', sans-serif; line-height: 1.6; color: #333; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background: linear-gradient(135deg, #FF8C00 0%, #FFA500 100%); 
                   color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }
        .content { background: #ffffff; padding: 30px; border: 1px solid #e0e0e0; }
        .quote-box { background: #FFF9E6; border-left: 4px solid #FF8C00; 
                      padding: 20px; margin: 20px 0; }
        .price { font-size: 32px; color: #28A745; font-weight: bold; }
        .button { background: #1976D2; color: white; padding: 15px 30px; 
                   text-decoration: none; border-radius: 5px; display: inline-block; 
                   margin: 20px 0; }
        .footer { text-align: center; padding: 20px; color: #666; font-size: 12px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🌞 New Solar Quote Received!</h1>
        </div>
        <div class='content'>
            <p>Dear " + recipientName + @",</p>
            
            <p>Great news! You've received a new quote for your solar installation request on <strong>SolarConnect</strong>.</p>
            
            <div class='quote-box'>
                <h3 style='margin-top: 0; color: #FF8C00;'>Quote Details</h3>
                <p><strong>Vendor:</strong> " + vendorCompany + @"</p>
                <p><strong>Total Price:</strong> <span class='price'>$" + quotePrice.ToString("N2") + @"</span></p>
                <p style='color: #666; font-size: 14px;'>📎 Complete quote details are attached as a PDF</p>
            </div>
            
            <p>The vendor has carefully reviewed your requirements and prepared a customized solar solution for you.</p>
            
            <p><strong>Next Steps:</strong></p>
            <ul>
                <li>Review the attached PDF quote carefully</li>
                <li>Compare with other quotes you've received</li>
                <li>Log in to your SolarConnect account to accept or discuss further</li>
            </ul>
            
            <center>
                <a href='https://localhost:7001' class='button'>View in SolarConnect</a>
            </center>
            
            <p style='margin-top: 30px; font-size: 14px; color: #666;'>
                Have questions? The vendor's contact information is included in the PDF attachment.
            </p>
        </div>
        <div class='footer'>
            <p>© 2025 SolarConnect Platform | Connecting Solar Solutions in Lebanon</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

                // Attach PDF
                var fileName = "Quote_" + vendorCompany.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyyMMdd") + ".pdf";
                builder.Attachments.Add(fileName, quotePdf, new ContentType("application", "pdf"));

                message.Body = builder.ToMessageBody();

                // Send Email
                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort,
                        _emailSettings.EnableSSL ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                    await client.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                // Log the error
                throw new Exception("Failed to send email: " + ex.Message, ex);
            }
        }
    }
}