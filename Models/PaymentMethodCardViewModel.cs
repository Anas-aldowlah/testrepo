using YAGOT_2._0.Services;

namespace YAGOT_2._0.Models;

public sealed record PaymentMethodCardViewModel(
    PaymentMethodPresentation PaymentMethod,
    string? StateDescription = null,
    string StateKind = "pending");
