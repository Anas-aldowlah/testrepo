using System.Text.Json;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services
{
    public class StoreSettingsService
    {
        private readonly string _filePath;
        private readonly ILogger<StoreSettingsService> _logger;

        public StoreSettingsService(IWebHostEnvironment env, ILogger<StoreSettingsService> logger)
        {
            _logger = logger;
            // The JSON file will be stored in a 'Data' folder at the root of the project.
            var dataDir = Path.Combine(env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            _filePath = Path.Combine(dataDir, "storesettings.json");
        }

        public StoreSettings GetSettings()
        {
            if (!File.Exists(_filePath))
            {
                var defaultSettings = new StoreSettings();
                SaveSettings(defaultSettings); // Create the file with default values if it doesn't exist
                return defaultSettings;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<StoreSettings>(json) ?? new StoreSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading store settings from JSON file. Returning default settings.");
                return new StoreSettings();
            }
        }

        public void SaveSettings(StoreSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving store settings to JSON file.");
                throw; // Rethrow to let the controller handle/show error if needed
            }
        }
    }
}
