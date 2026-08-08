using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace YAGOT_2._0.Services;

public sealed class SmtpPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly EmailOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SmtpPasswordResetEmailSender> _logger;

    public SmtpPasswordResetEmailSender(
        IOptions<EmailOptions> options,
        IWebHostEnvironment environment,
        ILogger<SmtpPasswordResetEmailSender> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendResetLinkAsync(
        string email,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        var smtp = _options.Smtp;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = "إعادة تعيين كلمة المرور - ياقوت",
                Body = $"استخدم الرابط التالي لإعادة تعيين كلمة المرور. تنتهي صلاحية الرابط خلال 15 دقيقة:\n\n{resetUrl}",
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(email));

            // Gmail displays app passwords in groups. SMTP expects the actual
            // 16-character value, so remove display whitespace before auth.
            var normalizedPassword = string.Concat(
                smtp.Password.Where(character => !char.IsWhiteSpace(character)));

            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = smtp.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtp.UserName, normalizedPassword),
                Timeout = smtp.TimeoutMilliseconds
            };

            _logger.LogInformation(
                "Sending password reset email through SMTP host {Host}:{Port} with SSL={EnableSsl} to {Recipient}.",
                smtp.Host,
                smtp.Port,
                smtp.EnableSsl,
                email);

            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Password reset email accepted by the SMTP server for {Recipient}.", email);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(
                ex,
                "SMTP delivery failed for password reset email to {Recipient}. Host={Host}, Port={Port}, SSL={EnableSsl}, StatusCode={StatusCode}.",
                email,
                smtp.Host,
                smtp.Port,
                smtp.EnableSsl,
                ex.StatusCode);
            LogDevelopmentFallback(resetUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Password reset email failed before or during SMTP delivery to {Recipient}. Host={Host}, Port={Port}, ExceptionType={ExceptionType}.",
                email,
                smtp.Host,
                smtp.Port,
                ex.GetType().FullName);
            LogDevelopmentFallback(resetUrl);
            throw;
        }
    }

    private void LogDevelopmentFallback(string resetUrl)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("[YAGOT DEVELOPMENT] SMTP delivery failed. Use this password reset URL:");
        Console.WriteLine(resetUrl);
        Console.WriteLine();
    }
}
