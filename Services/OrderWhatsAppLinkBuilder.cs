using System.Text;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

public static class OrderWhatsAppLinkBuilder
{
    public static string? BuildCustomerOrderNotification(Order order, string? adminPhone)
    {
        ArgumentNullException.ThrowIfNull(order);

        var phone = NormalizePhone(adminPhone);
        if (phone.Length == 0)
            return null;

        var message = new StringBuilder()
            .Append("مرحباً، أود إشعاركم بطلب جديد. رقم الطلب: #")
            .Append(order.Id);

        if (!string.IsNullOrWhiteSpace(order.Trackingnumber))
        {
            message.Append("، رقم التتبع: ").Append(order.Trackingnumber.Trim());
        }

        return $"https://wa.me/{phone}?text={Uri.EscapeDataString(message.ToString())}";
    }

    public static string? BuildCustomerConflictUpdateNotification(Order order, string? adminPhone)
    {
        ArgumentNullException.ThrowIfNull(order);

        var phone = NormalizePhone(adminPhone);
        if (phone.Length == 0)
            return null;

        var message = new StringBuilder()
            .Append("مرحباً، أود إشعاركم بتحديث قراري للطلب رقم #")
            .Append(order.Id);

        if (!string.IsNullOrWhiteSpace(order.Trackingnumber))
            message.Append("، رقم التتبع: ").Append(order.Trackingnumber.Trim());

        return $"https://wa.me/{phone}?text={Uri.EscapeDataString(message.ToString())}";
    }

    public static string? Build(Order order, string? accountPhone)
    {
        ArgumentNullException.ThrowIfNull(order);

        var phone = NormalizePhone(order.Deliveryorder?.Phonenumber);
        if (phone.Length == 0)
            phone = NormalizePhone(accountPhone);
        if (phone.Length == 0)
            return null;

        var message = BuildMessage(order);
        return message == null ? null : $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";
    }

    private static string? BuildMessage(Order order)
    {
        var reference = string.IsNullOrWhiteSpace(order.Trackingnumber)
            ? $"#{order.Id}"
            : order.Trackingnumber;

        if (order.Workflowstate == OrderWorkflowStates.ConflictAwaitingDecision)
        {
            var message = new StringBuilder()
                .AppendLine("مرحباً، نحتاج تأكيد قرارك بشأن الكميات المتوفرة للطلب " + reference + ":");
            foreach (var item in order.Orderitems
                .Where(item => (item.UnavailableQuantity ?? 0) > 0)
                .OrderBy(item => item.Id))
            {
                var available = Math.Max(0, item.Quantity - (item.UnavailableQuantity ?? 0));
                var name = item.Product?.Name ?? $"المنتج {item.Productid}";
                message.AppendLine($"- {name}: المتوفر {available} من {item.Quantity}");
            }

            message.Append("يرجى فتح تفاصيل الطلب واختيار قرارك. لن يُخصم المخزون قبل حفظ القرار.");
            return message.ToString();
        }

        return order.Status switch
        {
            OrderStatuses.Paid => $"مرحباً، تم التحقق من دفع الطلب {reference} واعتماد منتجاته، وسيبدأ التجهيز.",
            OrderStatuses.Processed => $"مرحباً، طلبك {reference} قيد التجهيز الآن.",
            OrderStatuses.Shipped => $"مرحباً، تم شحن طلبك {reference} وهو في طريقه إليك.",
            OrderStatuses.Delivered => $"مرحباً، تم تسجيل تسليم الطلب {reference}. نشكرك لاختيار ياقوت.",
            OrderStatuses.Cancelled => $"مرحباً، تم إلغاء الطلب {reference}. إذا كان لديك استفسار يرجى التواصل معنا.",
            _ => null
        };
    }

    private static string NormalizePhone(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsDigit));
}
