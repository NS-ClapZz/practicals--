using ShopSystem.Application.Abstractions;
using ShopSystem.Core.Orders;

namespace ShopSystem.Application.Orders;

public sealed class OrderService(IShopRepository repository)
{
    public Order CreateOrder(string customerName, IEnumerable<int> productIds)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("Customer name is required.", nameof(customerName));
        }

        ArgumentNullException.ThrowIfNull(productIds);

        var requestedProductIds = productIds.ToHashSet();
        var selectedProducts = repository.Products
            .Where(product => requestedProductIds.Contains(product.Id))
            .ToArray();

        if (selectedProducts.Length == 0)
        {
            throw new InvalidOperationException("Order must contain at least one product.");
        }

        var order = new Order
        {
            Id = repository.Orders.Count + 1,
            CustomerName = customerName.Trim(),
            Products = selectedProducts
        };

        repository.AddOrder(order);
        return order;
    }
}

