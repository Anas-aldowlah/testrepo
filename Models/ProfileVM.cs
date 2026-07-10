using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public class ProfileVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الاسم الكامل مطلوب.")]
    [MinLength(2, ErrorMessage = "الاسم يجب أن يكون حرفين على الأقل.")]
    [MaxLength(255, ErrorMessage = "الاسم طويل جداً.")]
    public string Name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
    public string? Email { get; set; }

    // للعرض فقط — لا يتم تعديله من هذا النموذج
    public string Role { get; set; } = string.Empty;

    // للعرض فقط — لا يتم تعديله من هذا النموذج
    public DateTime? CreatedAt { get; set; }
}