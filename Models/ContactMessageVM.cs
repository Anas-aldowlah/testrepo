using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public class ContactMessageVM
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب.")]
    [MinLength(2, ErrorMessage = "الاسم يجب أن يكون حرفين على الأقل.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "رقم الجوال غير صالح.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "الموضوع مطلوب.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "الرسالة مطلوبة.")]
    [MinLength(10, ErrorMessage = "الرسالة يجب أن تكون 10 أحرف على الأقل.")]
    public string Message { get; set; } = string.Empty;
}