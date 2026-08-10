namespace YAGOT_2._0.Services;

public sealed class CartConcurrencyException : Exception
{
    public CartConcurrencyException(string message)
        : base(message)
    {
    }

    public CartConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
