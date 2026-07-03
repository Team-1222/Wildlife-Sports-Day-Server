using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Wildlife_Sports_Day_Server.Services;

public class GmailEmailSender(IConfiguration configuration, ILogger<GmailEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var gmailAddress = configuration["Gmail:Address"];
        if (string.IsNullOrWhiteSpace(gmailAddress))
        {
            throw new InvalidOperationException("Gmail address is not configured.");
        }

        var gmailAppPassword = configuration["Gmail:AppPassword"];
        if (string.IsNullOrWhiteSpace(gmailAppPassword))
        {
            throw new InvalidOperationException("Gmail app password is not configured.");
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(gmailAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(gmailAddress, gmailAppPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        logger.LogInformation("Sent email message through SMTP");
    }
}
