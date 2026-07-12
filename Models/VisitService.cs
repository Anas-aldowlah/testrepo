using System.Net.Http.Json;
using UAParser;

namespace YAGOT_2._0.Models
{
    public class VisitService : IVisitService
    {
        private readonly NeondbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;

        public VisitService(
            NeondbContext db,
            IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SaveVisitAsync(HttpContext context,string? name=null)
        {
            try
            {
                // اسم الزائر
                string visitorName = name != null
                    ? name!
                    : "زائر";



                // User Agent
                string userAgent = context.Request.Headers["User-Agent"].ToString();

                var parser = Parser.GetDefault();
                var client = parser.Parse(userAgent);

                string browser = client.UA.Family;

                string device;

                if (client.Device.Family != "Other")
                    device = client.Device.Family;
                else if (client.OS.Family.Contains("Android") ||
                         client.OS.Family.Contains("iOS"))
                    device = "Mobile";
                else
                    device = "Desktop";

                // الحصول على IP الحقيقي
                string ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(ip))
                {
                    ip = ip.Split(',')[0].Trim();
                }
                else
                {
                    ip = context.Connection.RemoteIpAddress?.ToString() ?? "";
                }

                // أثناء التطوير المحلي
                if (ip == "::1" || ip == "127.0.0.1")
                    ip = "";

                string country = "";
                string governorate = "";
                string city = "";

                try
                {
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        var http = _httpClientFactory.CreateClient();

                        var result = await http.GetFromJsonAsync<IpWhoIsResponse>(
                            $"https://ipwho.is/{ip}");

                        if (result != null && result.Success)
                        {
                            country = result.Country ?? "";
                            governorate = result.Region ?? "";
                            city = result.City ?? "";
                        }
                    }
                }
                catch
                {
                    // تجاهل خطأ خدمة تحديد الموقع
                }

                var visit = new Visit
                {
                    Visitdate = DateTime.UtcNow.AddHours(3),
                    Visitorname = visitorName,
                    Country = country,
                    Governorate = governorate,
                    City = city,
                    Device = device,
                    Browser = browser
                };

                _db.Visits.Add(visit);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // تجاهل أي خطأ حتى لا يتوقف الموقع
            }
        }
    }
}
