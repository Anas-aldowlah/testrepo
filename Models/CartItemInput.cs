using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public class AddCartItemInput
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "A valid product is required.")]
    public int ProductId { get; set; }

    [Required]
    [Range(0, 1000000, ErrorMessage = "Quantity must be between 0 and 1,000,000.")]
    public int Quantity { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Retail price selection is invalid.")]
    public int? RetailPriceId { get; set; }
}

public sealed class UpdateCartItemInput : AddCartItemInput
{
    [Range(0, int.MaxValue, ErrorMessage = "Cart item selection is invalid.")]
    public int CartItemId { get; set; }

    [Range(0, 1000000, ErrorMessage = "Expected quantity must be between 0 and 1,000,000.")]
    public int? ExpectedQuantity { get; set; }
}
