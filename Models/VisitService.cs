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

        public async Task SaveVisitAsync(HttpContext context)
        {
            try
            {
                // اسم المستخدم
                string visitorName = context.User.Identity?.IsAuthenticated == true
                    ? context.User.Identity.Name!
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

                // IP
                string ip = context.Connection.RemoteIpAddress?.ToString() ?? "";

                // أثناء التطوير على localhost
                if (ip == "::1" || ip == "127.0.0.1")
                    ip = "";

                string country = "";
                string governorate = "";
                string city = "";

                try
                {
                    var http = _httpClientFactory.CreateClient();

                    var result = await http.GetFromJsonAsync<IpApiResponse>(
                        $"http://ip-api.com/json/{ip}?fields=country,regionName,city");

                    if (result != null)
                    {
                        country = result.Country ?? "";
                        governorate = result.RegionName ?? "";
                        city = result.City ?? "";
                    }
                }
                catch
                {
                }

                var visit = new Visit   
                {
                    Visitdate = DateTime.Now,
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
