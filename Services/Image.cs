using Microsoft.AspNetCore.Hosting;
using YAGOT_2._0.Models;
namespace YAGOT_2._0.Services
{
    public class Image
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public Image(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string?> UploadImage(IFormFile? imageFile, string subFolder)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            // تحديد المسار (wwwroot/images/subFolder)
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", subFolder);

            // إنشاء المجلد إذا لم يكن موجوداً
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // توليد اسم فريد
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // حفظ الملف
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return uniqueFileName;
        }

        public async Task<string?> UpdateImage(IFormFile? imageFile, string subFolder,string ExistingImage)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            // تحديد المسار (wwwroot/images/subFolder)
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", subFolder);

            // إنشاء المجلد إذا لم يكن موجوداً
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // توليد اسم فريد
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // حفظ الملف
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }


            // 3. حذف الصورة القديمة من السيرفر (فقط إذا نجح الرفع الجديد)
            if (!string.IsNullOrEmpty(ExistingImage))
            {
                // تنظيف المسار لضمان دمج صحيح
                string oldFileName = Path.GetFileName(ExistingImage);
                string oldPath = Path.Combine(uploadsFolder, oldFileName);

                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }
            return "/images/products/"+ uniqueFileName;
        }
    }
}
