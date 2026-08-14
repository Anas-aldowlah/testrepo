using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public class CheckoutVM
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public Cart Cart { get; set; } = new();

    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public List<CheckoutCartItemSnapshot> SubmittedCartItems { get; set; } = [];

    public decimal SubmittedCartTotal { get; set; }

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
    [RegularExpression(@"^[\u0600-\u06FFa-zA-Z\s]+$", ErrorMessage = "المحافظة يجب أن تحتوي على أحرف فقط ولا تقبل أرقاماً.")]
    public string Governorate
    {
        get => _governorate;
        set => _governorate = value?.Trim() ?? string.Empty;
    }

    private string _city = string.Empty;

    [Required(ErrorMessage = "المدينة مطلوبة.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "المدينة مطلوبة.")]
    [RegularExpression(@"^[\u0600-\u06FFa-zA-Z\s]+$", ErrorMessage = "المدينة يجب أن تحتوي على أحرف فقط ولا تقبل أرقاماً.")]
    public string City
    {
        get => _city;
        set => _city = value?.Trim() ?? string.Empty;
    }

    private string _district = string.Empty;

    [Required(ErrorMessage = "اسم المستلم مطلوب.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "اسم المستلم مطلوب.")]
    [RegularExpression(@"^[\u0600-\u06FFa-zA-Z\s]+$", ErrorMessage = "اسم المستلم يجب أن يحتوي على أحرف فقط ولا يقبل أرقاماً.")]
    public string District
    {
        get => _district;
        set => _district = value?.Trim() ?? string.Empty;
    }

    private string _street = string.Empty;

    [Required(ErrorMessage = "رقم جوال المستلم مطلوب.")]
    [StringLength(9, MinimumLength = 9, ErrorMessage = "رقم الجوال يجب أن يتكون من 9 أرقام بالضبط.")]
    [RegularExpression(@"^7[01378][0-9]{7}$", ErrorMessage = "رقم الجوال يجب أن يتكون من 9 أرقام ويبدأ بـ 70 أو 71 أو 73 أو 77 أو 78.")]
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
    public string PaymentMethod
    {
        get => _paymentMethod;
        set => _paymentMethod = value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public Microsoft.AspNetCore.Http.IFormFile? ReceiptImage { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public IReadOnlyList<Paymentmethod> PaymentMethods { get; set; } = [];
}

public sealed class CheckoutCartItemSnapshot
{
    public int ProductId { get; set; }

    public int? RetailPriceId { get; set; }

    public int? RetailSizeMl { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
