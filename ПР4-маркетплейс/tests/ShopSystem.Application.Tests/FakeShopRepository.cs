using ShopSystem.Application.Abstractions;
using ShopSystem.Core.Catalog;
using ShopSystem.Core.Orders;

namespace ShopSystem.Application.Tests;

internal sealed class FakeShopRepository : IShopRepository
{
    private readonly List<Product> _products = [];
    private readonly List<Order> _orders = [];

    public IReadOnlyCollection<Product> Products => _products;

    public IReadOnlyCollection<Order> Orders => _orders;

    public void AddProduct(Product product) => _products.Add(product);

    public void AddOrder(Order order) => _orders.Add(order);
}

