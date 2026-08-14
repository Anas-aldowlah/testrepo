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
                "Sending password reset email through SMTP host {Host}:{Port} with SSL={EnableSsl}; recipient suppressed.",
                smtp.Host,
                smtp.Port,
                smtp.EnableSsl);

            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Password reset email accepted by the SMTP server; recipient suppressed.");
        }
        catch (SmtpException ex)
        {
            _logger.LogError(
                "SMTP delivery failed for a password reset email. Host={Host}, Port={Port}, SSL={EnableSsl}, StatusCode={StatusCode}; recipient and exception details suppressed.",
                smtp.Host,
                smtp.Port,
                smtp.EnableSsl,
                ex.StatusCode);
            LogDevelopmentFallback();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Password reset email failed before or during SMTP delivery. Host={Host}, Port={Port}, ExceptionType={ExceptionType}; recipient and exception details suppressed.",
                smtp.Host,
                smtp.Port,
                ex.GetType().FullName);
            LogDevelopmentFallback();
            throw;
        }
    }

    private void LogDevelopmentFallback()
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        _logger.LogWarning("Development SMTP fallback was suppressed because reset URLs contain secret tokens.");
    }
}
