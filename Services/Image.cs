using Microsoft.AspNetCore.Hosting;
using YAGOT_2._0.Models;
namespace YAGOT_2._0.Services
{
    public class Image
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        public Image(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string?> UploadImage(IFormFile? imageFile, string subFolder)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            if (GetValidationError(imageFile) != null)
                return null;

            string extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            // تحديد المسار
            string uploadsFolder = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "images",
                subFolder);

            // إنشاء المجلد إذا لم يكن موجوداً
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // اسم جديد يحل مشكلة العربية
            string uniqueFileName = $"{Guid.NewGuid()}{extension}";

            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // حفظ الملف
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return uniqueFileName;
        }

        public static string? GetValidationError(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            if (imageFile.Length > MaxFileSize)
                return "يجب ألا يتجاوز حجم الصورة 5 ميجابايت.";

            string extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            return AllowedExtensions.Contains(extension)
                ? null
                : "صيغة الصورة غير مدعومة. استخدم صورة بصيغة مدعومة.";
        }

        public async Task<string?> UpdateImage(
            IFormFile? imageFile,
            string subFolder,
            string ExistingImage)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            if (imageFile.Length > MaxFileSize)
                return null;

            string extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
                return null;

            // تحديد المسار
            string uploadsFolder = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "images",
                subFolder);

            // إنشاء المجلد إذا لم يكن موجوداً
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // اسم جديد يحل مشكلة العربية
            string uniqueFileName = $"{Guid.NewGuid()}{extension}";

            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // حفظ الملف
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            // حذف الصورة القديمة بعد نجاح رفع الجديدة
            if (!string.IsNullOrEmpty(ExistingImage))
            {
                string oldFileName = Path.GetFileName(ExistingImage);
                string oldPath = Path.Combine(uploadsFolder, oldFileName);

                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            return $"/images/{subFolder}/{uniqueFileName}";
        }
    }
}
