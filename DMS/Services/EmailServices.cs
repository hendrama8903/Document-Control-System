using MDC.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Mail;

public class EmailService
{
    private readonly EmailConfiguration Config;
    private IWebHostEnvironment Environment;

    public EmailService(IOptions<EmailConfiguration> options, IWebHostEnvironment environment)
    {
        Config = options.Value;
        Environment = environment;
    }

    public async Task SendEmailAsync(List<string> toAddresses, string subject, string title, string body, string buttonLink)
    {
        using (var client = new SmtpClient(Config.smtpServer))
        {
            client.Port = (int)Config.port;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(Config.username, Config.password);
            client.EnableSsl = true;

            var message = new MailMessage
            {
                From = new MailAddress(Config.username),
                Subject = subject,
                Body = GetBodyTemplate(title, body, buttonLink),
                IsBodyHtml = true
            };

            foreach (var toAddress in toAddresses)
            {
                message.To.Add(toAddress);
            }

            try
            {
                await client.SendMailAsync(message);
                Console.WriteLine("Email sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                throw; // Re-throw the exception for higher-level handling
            }
        }

        string GetBodyTemplate(string title, string body, string buttonLink)
        {
            string templateFilePath = Path.Combine(Environment.WebRootPath, "Document/EmailTemplate/EmailTemplate.html");
            CultureInfo cultureInfo = new CultureInfo("id-ID"); // Budaya Indonesia
            string dateNow = DateTime.Now.ToString("dddd, d MMMM yyyy", cultureInfo);

            // Read the HTML content from the template file
            string htmlContent = File.ReadAllText(templateFilePath);

            // Replace placeholders with actual button link and text
            htmlContent = htmlContent.Replace("{title}", title).Replace("{body}", body).Replace("{buttonLink}", buttonLink).Replace("{dateNow}", dateNow);

            return htmlContent;
        }
    }
}
