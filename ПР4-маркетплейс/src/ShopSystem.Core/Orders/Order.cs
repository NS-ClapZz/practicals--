using ShopSystem.Core.Catalog;

namespace ShopSystem.Core.Orders;

public sealed class Order
{
    public int Id { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public IReadOnlyCollection<Product> Products { get; init; } = Array.Empty<Product>();

    public decimal TotalAmount => Products.Sum(product => product.Price);
}

