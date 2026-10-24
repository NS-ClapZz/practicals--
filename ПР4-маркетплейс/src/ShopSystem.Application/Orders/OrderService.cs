using ShopSystem.Application.Abstractions;
using ShopSystem.Core.Orders;

namespace ShopSystem.Application.Orders;

public sealed class OrderService(IShopRepository repository)
{
    public Order CreateOrder(string customerName, IEnumerable<int> productIds)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("Имя покупателя обязательно для заполнения.", nameof(customerName));
        }

        ArgumentNullException.ThrowIfNull(productIds);

        var requestedProductIds = productIds.ToHashSet();
        var selectedProducts = repository.Products
            .Where(p => requestedProductIds.Contains(p.Id))
            .ToArray();

        if (selectedProducts.Length == 0)
        {
            throw new InvalidOperationException("Заказ должен содержать хотя бы один существующий товар.");
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
