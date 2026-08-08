using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace YAGOT_2._0.Services
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<OtpService> _logger;

        private record OtpRecord(string Code, DateTime CreatedAt, int Attempts);

        public OtpService(IMemoryCache cache, ILogger<OtpService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<(bool Success, string Message, string? CodeForDev)> SendOtpAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 9)
            {
                return Task.FromResult((false, "رقم الجوال غير صالح.", (string?)null));
            }

            var cleanPhone = phone.Trim();
            var cooldownKey = $"OtpCooldown:{cleanPhone}";
            var otpKey = $"OtpCode:{cleanPhone}";

            if (_cache.TryGetValue(cooldownKey, out _))
            {
                return Task.FromResult((false, "يرجى الانتظار دقيقة واحدة قبل طلب رمز جديد.", (string?)null));
            }

            // Generate 6 digit numeric code
            var codeNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
            var code = codeNumber.ToString();

            var otpRecord = new OtpRecord(code, DateTime.UtcNow, 0);

            // Store code for 5 minutes
            _cache.Set(otpKey, otpRecord, TimeSpan.FromMinutes(5));

            // Set cooldown for 60 seconds
            _cache.Set(cooldownKey, true, TimeSpan.FromSeconds(60));

            _logger.LogInformation("[OTP SERVICE] Sent OTP {Code} to phone {Phone}", code, cleanPhone);
            Console.WriteLine($"[YAGOT OTP] Verification code for {cleanPhone} is: {code}");

            return Task.FromResult((true, "تم إرسال رمز التحقق بنجاح إلى رقم جوالك.", (string?)code));
        }

        public Task<(bool Success, string Message)> VerifyOtpAsync(string phone, string code)
        {
            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(code))
            {
                return Task.FromResult((false, "يرجى إدخال رقم الجوال ورمز التحقق."));
            }

            var cleanPhone = phone.Trim();
            var cleanCode = code.Trim();
            var otpKey = $"OtpCode:{cleanPhone}";

            if (!_cache.TryGetValue<OtpRecord>(otpKey, out var record) || record == null)
            {
                return Task.FromResult((false, "رمز التحقق انتهت صلاحيته أو لم يتم طلبه. يرجى إعادة الطلب."));
            }

            if (record.Attempts >= 3)
            {
                _cache.Remove(otpKey);
                return Task.FromResult((false, "تجاوزت عدد المحاولات المسموح بها. يرجى طلب رمز جديد."));
            }

            if (record.Code != cleanCode)
            {
                // Increment attempts
                var updatedRecord = record with { Attempts = record.Attempts + 1 };
                _cache.Set(otpKey, updatedRecord, TimeSpan.FromMinutes(5));

                var remaining = 3 - updatedRecord.Attempts;
                return Task.FromResult((false, $"رمز التحقق غير صحيح. المتبقي {remaining} محاولات."));
            }

            // Successfully verified -> clear cache key
            _cache.Remove(otpKey);
            _logger.LogInformation("[OTP SERVICE] Phone {Phone} successfully verified with OTP", cleanPhone);

            return Task.FromResult((true, "تم تحقق رقم الجوال بنجاح!"));
        }
    }
}
