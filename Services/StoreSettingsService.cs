using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services
{
    public class StoreSettingsService
    {
        private readonly NeondbContext _context;
        private readonly ILogger<StoreSettingsService> _logger;
        private static readonly PaymentMethodDefinition[] PaymentMethodDefinitions =
        [
            new("al-amqi", "العمقي", "ExchangeCompany"),
            new("bin-dawl", "بن دول", "Bank"),
            new("al-basiri", "البسيري", "ExchangeCompany"),
            new("other", "أخرى", "Other")
        ];

        public StoreSettingsService(NeondbContext context, ILogger<StoreSettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<StoreSettings> GetSettingsAsync()
        {
            try
            {
                var settings = await GetExistingSettingsAsync();
                if (settings == null)
                {
                    settings = new StoreSettings();
                    NormalizeSettings(settings);
                    _context.Storesettings.Add(ToEntity(settings));
                    await _context.SaveChangesAsync();
                }

                var paymentMethods = await _context.Paymentmethods
                    .AsNoTracking()
                    .Where(method => method.Storesettingsid == settings.Id)
                    .OrderBy(method => method.Id)
                    .ToListAsync();
                ApplyPaymentMethods(settings, paymentMethods);

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading store settings from database.");
                return new StoreSettings();
            }
        }

        public StoreSettings GetSettings()
        {
            return GetSettingsAsync().GetAwaiter().GetResult();
        }

        public async Task SaveSettingsAsync(StoreSettings settings)
        {
            try
            {
                NormalizeSettings(settings);

                var settingsRows = await _context.Storesettings
                    .OrderBy(row => row.Id)
                    .ToListAsync();

                Storesetting officialSettings;
                if (settingsRows.Count == 0)
                {
                    officialSettings = ToEntity(settings);
                    _context.Storesettings.Add(officialSettings);
                    _logger.LogInformation("Creating the first store settings row.");
                    await _context.SaveChangesAsync();
                }
                else
                {
                    officialSettings = settingsRows.First();
                    ApplySettings(officialSettings, settings);
                    _logger.LogInformation("Updating store settings row {SettingsId} from posted row {PostedSettingsId}.", officialSettings.Id, settings.Id);

                    if (settingsRows.Count > 1)
                    {
                        _context.Storesettings.RemoveRange(settingsRows.Skip(1));
                        _logger.LogWarning("Removed {DuplicateCount} duplicate store settings rows.", settingsRows.Count - 1);
                    }
                }

                await UpsertPaymentMethodAsync(officialSettings.Id, "al-amqi", settings.OmqiPaymentName, "العمقي", settings.OmqiAccountName, settings.OmqiAccountNumber, null);
                await UpsertPaymentMethodAsync(officialSettings.Id, "bin-dawl", settings.BinDowalPaymentName, "بن دول", settings.BinDowalAccountName, settings.BinDowalAccountNumber, null);
                await UpsertPaymentMethodAsync(officialSettings.Id, "al-basiri", settings.BusairiPaymentName, "البسيري", settings.BusairiAccountName, settings.BusairiAccountNumber, null);
                await UpsertPaymentMethodAsync(officialSettings.Id, "other", settings.OtherPaymentName, "أخرى", "طريقة دفع أخرى", string.Empty, settings.OtherPaymentInstructions);
                await SyncAdditionalPaymentMethodsAsync(officialSettings.Id, settings.AdditionalPaymentMethods);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving store settings to database.");
                throw;
            }
        }

        public void SaveSettings(StoreSettings settings)
        {
            SaveSettingsAsync(settings).GetAwaiter().GetResult();
        }

        private static void NormalizeSettings(StoreSettings settings)
        {
            settings.WhatsAppNumber = string.Concat((settings.WhatsAppNumber ?? string.Empty).Where(char.IsDigit));
            settings.ContactEmail = (settings.ContactEmail ?? string.Empty).Trim();
            settings.InstagramLink = (settings.InstagramLink ?? string.Empty).Trim();
            settings.TwitterLink = (settings.TwitterLink ?? string.Empty).Trim();
            settings.TikTokLink = (settings.TikTokLink ?? string.Empty).Trim();
            settings.HeroMarketingText = settings.HeroMarketingText ?? string.Empty;
            settings.HeroMarketingDesc = settings.HeroMarketingDesc ?? string.Empty;
            settings.OmqiPaymentName = NormalizePaymentName(settings.OmqiPaymentName, "العمقي");
            settings.OmqiAccountName = settings.OmqiAccountName ?? string.Empty;
            settings.OmqiAccountNumber = settings.OmqiAccountNumber ?? string.Empty;
            settings.BusairiPaymentName = NormalizePaymentName(settings.BusairiPaymentName, "البسيري");
            settings.BusairiAccountName = settings.BusairiAccountName ?? string.Empty;
            settings.BusairiAccountNumber = settings.BusairiAccountNumber ?? string.Empty;
            settings.BinDowalPaymentName = NormalizePaymentName(settings.BinDowalPaymentName, "بن دول");
            settings.BinDowalAccountName = settings.BinDowalAccountName ?? string.Empty;
            settings.BinDowalAccountNumber = settings.BinDowalAccountNumber ?? string.Empty;
            settings.OtherPaymentName = NormalizePaymentName(settings.OtherPaymentName, "أخرى");
            settings.OtherPaymentInstructions = settings.OtherPaymentInstructions ?? string.Empty;
        }

        private static string NormalizePaymentName(string? name, string fallback)
        {
            var trimmedName = name?.Trim();
            return string.IsNullOrWhiteSpace(trimmedName) ? fallback : trimmedName;
        }

        private static void ApplySettings(Storesetting target, StoreSettings source)
        {
            target.Whatsappnumber = source.WhatsAppNumber;
            target.Contactemail = source.ContactEmail;
            target.Instagramlink = source.InstagramLink;
            target.Twitterlink = source.TwitterLink;
            target.Tiktoklink = source.TikTokLink;
            target.Featuredcategoryid = source.FeaturedCategoryId;
            target.Heromarketingtext = source.HeroMarketingText;
            target.Heromarketingdesc = source.HeroMarketingDesc;
        }

        private static Storesetting ToEntity(StoreSettings settings)
        {
            return new Storesetting
            {
                Whatsappnumber = settings.WhatsAppNumber,
                Contactemail = settings.ContactEmail,
                Instagramlink = settings.InstagramLink,
                Twitterlink = settings.TwitterLink,
                Tiktoklink = settings.TikTokLink,
                Featuredcategoryid = settings.FeaturedCategoryId,
                Heromarketingtext = settings.HeroMarketingText,
                Heromarketingdesc = settings.HeroMarketingDesc
            };
        }

        public async Task<IReadOnlyList<Paymentmethod>> GetCheckoutPaymentMethodsAsync()
        {
            var settings = await GetExistingSettingsAsync();
            var settingsId = settings?.Id ?? 1;
            var storedMethods = await _context.Paymentmethods
                .AsNoTracking()
                .Where(method => method.Storesettingsid == settingsId)
                .OrderBy(method => method.Id)
                .ToListAsync();

            var builtInMethods = PaymentMethodDefinitions
                .Select(definition =>
                {
                    var method = FindPaymentMethod(storedMethods, definition.Code, definition.Name, definition.LegacyType);
                    return new Paymentmethod
                    {
                        Type = definition.Code,
                        Name = method?.Name?.Trim() is { Length: > 0 } storedName ? storedName : definition.Name,
                        Accountholdername = method?.Accountholdername ?? string.Empty,
                        Accountnumber = method?.Accountnumber ?? string.Empty,
                        Instructions = method?.Instructions,
                        Isactive = method?.Isactive ?? true,
                        Storesettingsid = settingsId
                    };
                })
                .ToList();

            var additionalMethods = storedMethods
                .Where(method => method.Isactive && !IsBuiltInPaymentMethod(method))
                .ToList();

            return builtInMethods.Concat(additionalMethods).ToList();
        }

        private static StoreSettings ToModel(Storesetting entity)
        {
            return new StoreSettings
            {
                Id = entity.Id,
                WhatsAppNumber = entity.Whatsappnumber ?? string.Empty,
                ContactEmail = entity.Contactemail ?? string.Empty,
                InstagramLink = entity.Instagramlink ?? string.Empty,
                TwitterLink = entity.Twitterlink ?? string.Empty,
                TikTokLink = entity.Tiktoklink ?? string.Empty,
                FeaturedCategoryId = entity.Featuredcategoryid,
                HeroMarketingText = entity.Heromarketingtext ?? string.Empty,
                HeroMarketingDesc = entity.Heromarketingdesc ?? string.Empty
            };
        }

        private async Task<StoreSettings?> GetExistingSettingsAsync()
        {
            var settings = await _context.Storesettings
                .OrderBy(settings => settings.Id)
                .FirstOrDefaultAsync();
            return settings == null ? null : ToModel(settings);
        }

        private async Task UpsertPaymentMethodAsync(
            int settingsId,
            string code,
            string? postedName,
            string fallbackName,
            string? accountHolderName,
            string? accountNumber,
            string? instructions)
        {
            var definition = PaymentMethodDefinitions.First(definition => definition.Code == code);
            var methods = await _context.Paymentmethods
                .Where(method => method.Storesettingsid == settingsId)
                .ToListAsync();
            var method = FindPaymentMethod(methods, code, fallbackName, definition.LegacyType);

            if (method == null)
            {
                method = new Paymentmethod
                {
                    Storesettingsid = settingsId,
                    Isactive = true
                };
                _context.Paymentmethods.Add(method);
            }

            method.Type = code;
            method.Name = NormalizePaymentName(postedName, fallbackName);
            method.Accountholdername = accountHolderName ?? string.Empty;
            method.Accountnumber = accountNumber ?? string.Empty;
            method.Instructions = instructions;
        }

        private static void ApplyPaymentMethods(StoreSettings settings, IReadOnlyCollection<Paymentmethod> paymentMethods)
        {
            var alAmqi = FindPaymentMethod(paymentMethods, "al-amqi", "العمقي", "ExchangeCompany");
            var binDawl = FindPaymentMethod(paymentMethods, "bin-dawl", "بن دول", "Bank");
            var alBasiri = FindPaymentMethod(paymentMethods, "al-basiri", "البسيري", "ExchangeCompany");
            var other = FindPaymentMethod(paymentMethods, "other", "أخرى", "Other");

            settings.OmqiPaymentName = alAmqi?.Name ?? settings.OmqiPaymentName;
            settings.OmqiAccountName = alAmqi?.Accountholdername ?? settings.OmqiAccountName;
            settings.OmqiAccountNumber = alAmqi?.Accountnumber ?? settings.OmqiAccountNumber;
            settings.BinDowalPaymentName = binDawl?.Name ?? settings.BinDowalPaymentName;
            settings.BinDowalAccountName = binDawl?.Accountholdername ?? settings.BinDowalAccountName;
            settings.BinDowalAccountNumber = binDawl?.Accountnumber ?? settings.BinDowalAccountNumber;
            settings.BusairiPaymentName = alBasiri?.Name ?? settings.BusairiPaymentName;
            settings.BusairiAccountName = alBasiri?.Accountholdername ?? settings.BusairiAccountName;
            settings.BusairiAccountNumber = alBasiri?.Accountnumber ?? settings.BusairiAccountNumber;
            settings.OtherPaymentName = other?.Name ?? settings.OtherPaymentName;
            settings.OtherPaymentInstructions = other?.Instructions ?? settings.OtherPaymentInstructions;
            settings.AdditionalPaymentMethods = paymentMethods
                .Where(method => !IsBuiltInPaymentMethod(method))
                .Select(method => new PaymentMethodSetting
                {
                    Id = method.Id,
                    Type = method.Type,
                    Name = method.Name,
                    AccountHolderName = method.Accountholdername,
                    AccountNumber = method.Accountnumber,
                    Instructions = method.Instructions,
                    IsActive = method.Isactive
                })
                .ToList();
        }

        private static Paymentmethod? FindPaymentMethod(
            IEnumerable<Paymentmethod> paymentMethods,
            string code,
            string name,
            string legacyType)
        {
            return paymentMethods.FirstOrDefault(method =>
                string.Equals(method.Type, code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(method.Type, legacyType, StringComparison.OrdinalIgnoreCase) && method.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        private async Task SyncAdditionalPaymentMethodsAsync(int settingsId, IEnumerable<PaymentMethodSetting>? postedMethods)
        {
            var existingMethods = await _context.Paymentmethods
                .Where(method => method.Storesettingsid == settingsId)
                .ToListAsync();
            var additionalMethods = existingMethods
                .Where(method => !IsBuiltInPaymentMethod(method))
                .ToList();

            foreach (var postedMethod in postedMethods ?? [])
            {
                var method = postedMethod.Id.HasValue
                    ? additionalMethods.FirstOrDefault(existing => existing.Id == postedMethod.Id.Value)
                    : null;

                if (postedMethod.Delete)
                {
                    if (method != null)
                    {
                        _context.Paymentmethods.Remove(method);
                    }
                    continue;
                }

                var name = postedMethod.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (method == null)
                {
                    method = new Paymentmethod
                    {
                        Storesettingsid = settingsId,
                        Type = CreateCustomPaymentType(),
                        Isactive = true
                    };
                    _context.Paymentmethods.Add(method);
                }

                method.Name = name;
                method.Accountholdername = postedMethod.AccountHolderName?.Trim() ?? string.Empty;
                method.Accountnumber = postedMethod.AccountNumber?.Trim() ?? string.Empty;
                method.Instructions = null;
                method.Isactive = true;
            }
        }

        private static bool IsBuiltInPaymentMethod(Paymentmethod method)
        {
            return PaymentMethodDefinitions.Any(definition =>
                string.Equals(method.Type, definition.Code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(method.Type, definition.LegacyType, StringComparison.OrdinalIgnoreCase) && method.Name.Contains(definition.Name, StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains(definition.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static string CreateCustomPaymentType()
            => $"custom-{Guid.NewGuid():N}"[..39];

        private sealed record PaymentMethodDefinition(string Code, string Name, string LegacyType);
    }
}
