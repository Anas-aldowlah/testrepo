using System.Security.Cryptography;
using System.Text;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services
{
    public class DealingAPI
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DealingAPI> _logger;

        public DealingAPI(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<DealingAPI> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }
        public enum StatueSite
        {
        Developer = 1,ColsePlane = 2,Online = 3
        }
     
        public async Task<StatueSite> checkDeveloperMode(int siteID)
        {
            try
            {
                var data = await _httpClient.GetFromJsonAsync<List<SiteDto>>("SiteAPI/GetSites");

                var site = data?.FirstOrDefault(s => s.SiteId == siteID);
                if (site == null || string.IsNullOrWhiteSpace(site.status))
                {
                    _logger.LogWarning(
                        "The site-status service returned no valid record for site {SiteId}; access is failing closed.",
                        siteID);
                    return StatueSite.ColsePlane;
                }

                if (site.status.Equals("Offline", StringComparison.OrdinalIgnoreCase))
                {
                    return StatueSite.ColsePlane;
                }

                if (site.status.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    return StatueSite.Developer;
                }

                if (site.status.Equals("Online", StringComparison.OrdinalIgnoreCase))
                {
                    return StatueSite.Online;
                }

                _logger.LogWarning(
                    "The site-status service returned unknown status {Status} for site {SiteId}; access is failing closed.",
                    site.status,
                    siteID);
                return StatueSite.ColsePlane;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "The site-status service failed for site {SiteId}; access is failing closed.",
                    siteID);
                return StatueSite.ColsePlane;
            }
        }
        public string DecryptPhone(string? encryptedPhone)
        {
            if (string.IsNullOrWhiteSpace(encryptedPhone))
            {
                return string.Empty;
            }

            try
            {
                string key = _configuration["Encryption:Key"] ?? string.Empty;
                string iv = _configuration["Encryption:IV"] ?? string.Empty;

                using var aes = Aes.Create();

                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.IV = Encoding.UTF8.GetBytes(iv);

                using var decryptor = aes.CreateDecryptor();

                byte[] encryptedBytes = Convert.FromBase64String(encryptedPhone);
                byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
