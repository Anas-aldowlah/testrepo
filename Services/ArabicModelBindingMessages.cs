using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace YAGOT_2._0.Services;

public static class ArabicModelBindingMessages
{
    public static void Configure(DefaultModelBindingMessageProvider messages)
    {
        messages.SetMissingBindRequiredValueAccessor(_ => "هذا الحقل مطلوب.");
        messages.SetMissingKeyOrValueAccessor(() => "القيمة مطلوبة.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "بيانات الطلب مطلوبة.");
        messages.SetValueMustNotBeNullAccessor(_ => "هذا الحقل مطلوب.");
        messages.SetAttemptedValueIsInvalidAccessor((_, _) => "القيمة المدخلة غير صالحة.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => "القيمة المدخلة غير صالحة.");
        messages.SetUnknownValueIsInvalidAccessor(_ => "القيمة المدخلة غير صالحة.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "القيمة المدخلة غير صالحة.");
        messages.SetValueIsInvalidAccessor(_ => "القيمة المدخلة غير صالحة.");
        messages.SetValueMustBeANumberAccessor(_ => "يجب إدخال رقم صالح.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "يجب إدخال رقم صالح.");
    }
}
