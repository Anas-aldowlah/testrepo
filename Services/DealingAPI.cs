using System;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using YAGOT_2._0.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using YAGOT_2._0.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace YAGOT_2._0.Services
{
    public class DealingAPI
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public DealingAPI(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }
        public enum StatueSite
        {
        Developer = 1,ColsePlane =2,NotFound = 3
        }
     
        public async Task<StatueSite> checkDeveloperMode(int siteID)
         {
            try
            {
              
                var data = await _httpClient.GetFromJsonAsync<List<SiteDto>>("SiteAPI/GetSites");

                var statue = data.Where(s => s.SiteId == siteID).FirstOrDefault();
                 
              
                if (statue.status == "Offline" )
                {
                    return StatueSite.ColsePlane;
                }
                else if (statue.status == "Development")
                {
                    return StatueSite.Developer;
                }
                else
                {
                    return StatueSite.NotFound;

                }
              
                    
                    
                    
         
            }
            catch (Exception ex)
            {
                Console.WriteLine("ex.ToString() assil not found");
                return StatueSite.NotFound;
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
                string key = _configuration["Encryption:Key"];
                string iv = _configuration["Encryption:IV"];

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
