using ShopSystem.Application.Abstractions;
using ShopSystem.Core.Catalog;
using ShopSystem.Core.Orders;

namespace ShopSystem.Infrastructure.Persistence;

public sealed class InMemoryShopRepository : IShopRepository
{
    private readonly List<Product> _products = [];
    private readonly List<Order> _orders = [];

    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();

    public void AddProduct(Product product) => _products.Add(product);

    public void AddOrder(Order order) => _orders.Add(order);
}

