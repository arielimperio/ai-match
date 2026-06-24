using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using NackaMatchmaking.API.Data;
using Microsoft.EntityFrameworkCore;

namespace NackaMatchmaking.API.Services
{
    public class SendGridEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;

        public SendGridEmailService(IConfiguration config, ApplicationDbContext context)
        {
            _config = config;
            _context = context;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlContent)
        {
            var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var apiKeySetting = await _context.Settings.FindAsync(systemId, "SendGridApiKey");
            var fromEmailSetting = await _context.Settings.FindAsync(systemId, "SendGridFromEmail");
            var fromNameSetting = await _context.Settings.FindAsync(systemId, "EmailFromName");

            var apiKey = !string.IsNullOrWhiteSpace(apiKeySetting?.Value) ? apiKeySetting.Value : (_config["SendGrid:ApiKey"] ?? string.Empty);
            var fromEmail = !string.IsNullOrWhiteSpace(fromEmailSetting?.Value) ? fromEmailSetting.Value : (_config["SendGrid:FromEmail"] ?? "ariel@itmaskinen.se");
            var fromName = !string.IsNullOrWhiteSpace(fromNameSetting?.Value) ? fromNameSetting.Value : "Nacka Företagarträff";

            // --- DIAGNOSTIC LOGGING ---
            System.Console.WriteLine($"[EMAIL SEND] To: {toEmail}");
            System.Console.WriteLine($"[EMAIL SEND] From: {fromEmail} ({fromName})");
            System.Console.WriteLine($"[EMAIL SEND] Subject: {subject}");
            System.Console.WriteLine($"[EMAIL SEND] API Key source: {(apiKeySetting?.Value != null ? "DATABASE" : "appsettings.json")}");
            System.Console.WriteLine($"[EMAIL SEND] API Key (first 20 chars): {(apiKey.Length > 20 ? apiKey[..20] + "..." : apiKey)}");

            if (string.IsNullOrEmpty(apiKey))
            {
                System.Console.WriteLine("[EMAIL SEND] ERROR: API key is empty! Email will NOT be sent.");
                return;
            }

            try
            {
                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(fromEmail, fromName);
                var to = new EmailAddress(toEmail);
                var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
                
                var response = await client.SendEmailAsync(msg);
                var responseBody = await response.Body.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    System.Console.WriteLine($"[EMAIL SEND] SUCCESS: Email sent to {toEmail} (HTTP {(int)response.StatusCode})");
                }
                else
                {
                    System.Console.WriteLine($"[EMAIL SEND] FAILED: HTTP {(int)response.StatusCode} {response.StatusCode}");
                    System.Console.WriteLine($"[EMAIL SEND] Response body: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[EMAIL SEND] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
