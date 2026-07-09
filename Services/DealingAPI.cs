using System;
using System.Net.Http;
using YAGOT_2._0.Models;
using System.Security.Claims;

namespace YAGOT_2._0.Services
{
    public class DealingAPI
    {
        private readonly HttpClient _httpClient;
        
        public DealingAPI(HttpClient httpClient) {
            _httpClient = httpClient;
          
        }
        public enum StatueSite
        {
        Developer = 1,ColsePlane =2,NotFound = 3
        }
     
        public async Task<StatueSite> checkDeveloperMode(int siteID)
         {
            try
            {
              
                var data = await _httpClient.GetFromJsonAsync<List<SiteDto>>("https://controlpanelsite-assil.onrender.com/SiteAPI/GetSites");

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
    }
}
