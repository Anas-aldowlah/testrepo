using System;
using System.Globalization;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services.Promotions
{
    public static class PromotionTime
    {
        public const string YemenTimeZoneId = "Asia/Aden";

        public static TimeZoneInfo YemenTimeZone { get; } = ResolveYemenTimeZone();

        public static DateTimeOffset YemenLocalToUtc(DateTime yemenWallClock)
        {
            var unspecified = DateTime.SpecifyKind(yemenWallClock, DateTimeKind.Unspecified);

            if (YemenTimeZone.IsInvalidTime(unspecified))
            {
                throw new ArgumentException("The supplied local time does not exist.", nameof(yemenWallClock));
            }

            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, YemenTimeZone), TimeSpan.Zero);
        }

        public static DateTime UtcToYemenLocal(DateTimeOffset utcInstant) =>
            TimeZoneInfo.ConvertTime(utcInstant.ToUniversalTime(), YemenTimeZone).DateTime;

        public static string ToUtcIso8601(DateTimeOffset utcInstant) =>
            utcInstant.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        public static bool IsActive(Promotion promotion, DateTimeOffset utcNow) =>
            promotion.IsActive && promotion.StartDate <= utcNow && utcNow < promotion.EndDate;

        public static string GetStatusKey(Promotion promotion, DateTimeOffset utcNow)
        {
            if (!promotion.IsActive) return "disabled";
            if (utcNow < promotion.StartDate) return "upcoming";
            if (utcNow >= promotion.EndDate) return "expired";
            return "active";
        }

        private static TimeZoneInfo ResolveYemenTimeZone()
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(YemenTimeZoneId, out var timeZone))
            {
                return timeZone;
            }

            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(YemenTimeZoneId, out var windowsId) &&
                TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out timeZone))
            {
                return timeZone;
            }

            if (TimeZoneInfo.TryFindSystemTimeZoneById("Arab Standard Time", out timeZone))
            {
                return timeZone;
            }

            return TimeZoneInfo.CreateCustomTimeZone("UTC+03", TimeSpan.FromHours(3), "Arab Standard Time", "Arab Standard Time");
        }
    }
}
