using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public class CheckoutVM
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public Cart Cart { get; set; } = new();

    private string _customerName = string.Empty;

    [Required(ErrorMessage = "اسم العميل مطلوب.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "اسم العميل يجب أن يكون حرفين على الأقل.")]
    public string CustomerName
    {
        get => _customerName;
        set => _customerName = value?.Trim() ?? string.Empty;
    }

    private string _customerPhone = string.Empty;

    [Required(ErrorMessage = "رقم الجوال مطلوب.")]
    [Phone(ErrorMessage = "رقم الجوال غير صالح.")]
    [StringLength(20, MinimumLength = 7, ErrorMessage = "رقم الجوال غير صالح.")]
    public string CustomerPhone
    {
        get => _customerPhone;
        set => _customerPhone = value?.Trim() ?? string.Empty;
    }

    private string? _customerEmail;

    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
    public string? CustomerEmail
    {
        get => _customerEmail;
        set
        {
            var normalized = value?.Trim();
            _customerEmail = string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
    }

    private string _governorate = string.Empty;

    [Required(ErrorMessage = "المحافظة مطلوبة.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "المحافظة مطلوبة.")]
    [RegularExpression(@"^[\p{L}\s]+$", ErrorMessage = "المحافظة يجب أن تحتوي على أحرف فقط ولا تقبل أرقاماً.")]
    public string Governorate
    {
        get => _governorate;
        set => _governorate = value?.Trim() ?? string.Empty;
    }

    private string _city = string.Empty;

    [Required(ErrorMessage = "المدينة مطلوبة.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "المدينة مطلوبة.")]
    [RegularExpression(@"^[\p{L}\s]+$", ErrorMessage = "المدينة يجب أن تحتوي على أحرف فقط ولا تقبل أرقاماً.")]
    public string City
    {
        get => _city;
        set => _city = value?.Trim() ?? string.Empty;
    }

    private string _district = string.Empty;

    [Required(ErrorMessage = "اسم المستلم مطلوب.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "اسم المستلم مطلوب.")]
    [RegularExpression(@"^[\p{L}\s]+$", ErrorMessage = "اسم المستلم يجب أن يحتوي على أحرف فقط ولا يقبل أرقاماً.")]
    public string District
    {
        get => _district;
        set => _district = value?.Trim() ?? string.Empty;
    }

    private string _street = string.Empty;

    [Required(ErrorMessage = "رقم جوال المستلم مطلوب.")]
    [StringLength(9, MinimumLength = 9, ErrorMessage = "رقم الجوال يجب أن يتكون من 9 أرقام بالضبط.")]
    [RegularExpression(@"^[0-9]{9}$", ErrorMessage = "رقم الجوال يجب أن يحتوي على 9 أرقام فقط.")]
    public string Street
    {
        get => _street;
        set => _street = value?.Trim() ?? string.Empty;
    }

    private string? _deliveryNotes;

    [StringLength(500, ErrorMessage = "الملاحظات طويلة جداً.")]
    public string? DeliveryNotes
    {
        get => _deliveryNotes;
        set
        {
            var normalized = value?.Trim();
            _deliveryNotes = string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
    }

    private string _paymentMethod = string.Empty;

    [Required(ErrorMessage = "طريقة الدفع مطلوبة.")]
    [RegularExpression("^(al-amqi|bin-dawl|al-basiri|other)$", ErrorMessage = "الرجاء اختيار طريقة دفع صحيحة.")]
    public string PaymentMethod
    {
        get => _paymentMethod;
        set => _paymentMethod = value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public Microsoft.AspNetCore.Http.IFormFile? ReceiptImage { get; set; }
}
