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
            var encodedResetUrl = WebUtility.HtmlEncode(resetUrl);

            var htmlBody = $"""
        <!DOCTYPE html>
        <html lang="ar" dir="rtl">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
        </head>

        <body style="margin:0; padding:0; background-color:#f5f7fa;
                     font-family:Arial,Tahoma,sans-serif;">

            <div style="max-width:600px; margin:40px auto; padding:20px;">

                <div style="background:#ffffff;
                            border-radius:14px;
                            overflow:hidden;
                            border:1px solid #e5e7eb;">

                    <!-- صورة LogiCore -->
                    <div style="text-align:center;
                                background:#ffffff;
                                padding:25px 20px 10px;">

                        <img
                            src="https://i.ibb.co/twKp2ntM/logicore-bg1.png"
                            alt="LogiCore"
                            style="max-width:100%;
                                   width:100%;
                                   height:auto;
                                   display:block;
                                   margin:0 auto;
                                   border:0;">
                    </div>

                    <!-- محتوى الرسالة -->
                    <div style="padding:30px 35px;">

                        <h1 style="text-align:center;
                                   color:#1f2937;
                                   font-size:24px;
                                   margin:0 0 25px;">
                            إعادة تعيين كلمة المرور
                        </h1>

                        <p style="color:#4b5563;
                                  font-size:15px;
                                  line-height:1.9;
                                  text-align:right;">
                            تم طلب إعادة تعيين كلمة المرور لحسابك في
                            <strong>ياقوت</strong> وجميع منصات
                            <strong>LogiCore</strong>.
                        </p>

                        <p style="color:#4b5563;
                                  font-size:15px;
                                  line-height:1.9;
                                  text-align:right;">
                            إذا كنت أنت من طلب إعادة تعيين كلمة المرور،
                            اضغط على الزر التالي للمتابعة:
                        </p>

                        <!-- زر إعادة التعيين -->
                        <div style="text-align:center; margin:30px 0;">

                            <a href="{encodedResetUrl}"
                               style="display:inline-block;
                                      background:#2563eb;
                                      color:#ffffff;
                                      text-decoration:none;
                                      padding:14px 32px;
                                      border-radius:8px;
                                      font-size:16px;
                                      font-weight:bold;">
                                إعادة تعيين كلمة المرور
                            </a>

                        </div>

                        <!-- مدة الصلاحية -->
                        <div style="background:#f9fafb;
                                    border-radius:8px;
                                    padding:15px;
                                    margin-top:20px;">

                            <p style="margin:0;
                                      color:#6b7280;
                                      font-size:14px;
                                      line-height:1.8;
                                      text-align:center;">
                                رابط إعادة التعيين صالح لمدة
                                <strong>15 دقيقة فقط</strong>.
                            </p>

                        </div>

                        <p style="color:#6b7280;
                                  font-size:14px;
                                  line-height:1.8;
                                  margin-top:25px;
                                  text-align:right;">
                            إذا لم تطلب إعادة تعيين كلمة المرور،
                            يمكنك تجاهل هذه الرسالة بأمان.
                        </p>

                        <hr style="border:none;
                                   border-top:1px solid #e5e7eb;
                                   margin:30px 0;">

                        <p style="text-align:center;
                                  color:#9ca3af;
                                  font-size:12px;
                                  line-height:1.7;
                                  margin:0;">
                            هذه رسالة آلية من نظام ياقوت
                            وجميع منصات LogiCore.
                            <br>
                            يرجى عدم الرد على هذه الرسالة.
                        </p>

                    </div>

                </div>

            </div>

        </body>
        </html>
        """;

            var plainTextBody =
                "YAGOT - إعادة تعيين كلمة المرور\n\n" +
                "تم طلب إعادة تعيين كلمة المرور لحسابك في " +
                "ياقوت وجميع منصات LogiCore.\n\n" +
                "إذا كنت أنت من طلب إعادة تعيين كلمة المرور، " +
                "استخدم الرابط الموجود في رسالة البريد.\n\n" +
                "صلاحية الرابط: 15 دقيقة.\n\n" +
                "إذا لم تطلب إعادة تعيين كلمة المرور، يمكنك تجاهل هذه الرسالة.";

            using var message = new MailMessage
            {
                From = new MailAddress(
                    _options.FromAddress,
                    _options.FromName),

                Subject = "إعادة تعيين كلمة المرور - ياقوت | LogiCore",

                Body = htmlBody,

                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(email));

            // نسخة نصية للمستخدمين الذين لا يدعم بريدهم HTML
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    plainTextBody,
                    null,
                    System.Net.Mime.MediaTypeNames.Text.Plain));

            // نسخة HTML
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    htmlBody,
                    null,
                    System.Net.Mime.MediaTypeNames.Text.Html));

            // إعداد SMTP
            var normalizedPassword = string.Concat(
                smtp.Password.Where(character => !char.IsWhiteSpace(character)));

            using var client = new SmtpClient(
                smtp.Host,
                smtp.Port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = smtp.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    smtp.UserName,
                    normalizedPassword),
                Timeout = smtp.TimeoutMilliseconds
            };

            _logger.LogInformation(
                "Sending password reset email through SMTP host {Host}:{Port} with SSL={EnableSsl}; recipient suppressed.",
                smtp.Host,
                smtp.Port,
                smtp.EnableSsl);

            cancellationToken.ThrowIfCancellationRequested();

            // إرسال الرسالة فعليًا
            await client.SendMailAsync(
                message,
                cancellationToken);

            _logger.LogInformation(
                "Password reset email accepted by the SMTP server; recipient suppressed.");
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
                smtp.EnableSsl,
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
