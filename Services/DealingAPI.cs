using System.Security.Cryptography;
using System.Text;

namespace YAGOT_2._0.Services
{
    public class DealingAPI
    {
        private readonly IConfiguration _configuration;

        public DealingAPI(IConfiguration configuration)
        {
            _configuration = configuration;
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
