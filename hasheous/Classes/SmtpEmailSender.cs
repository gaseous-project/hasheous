using System.Net.Mail;
using Classes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpClient smtpClient;

    public SmtpEmailSender(SmtpClient smtpClient)
    {
        this.smtpClient = smtpClient;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(Config.EmailSMTPConfiguration.SendAs),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true,
        };
        mailMessage.To.Add(email);

        await smtpClient.SendMailAsync(mailMessage);
    }
}