using ShopSystem.Core.Catalog;
using ShopSystem.Core.Orders;

namespace ShopSystem.Application.Abstractions;

public interface IShopRepository
{
    IReadOnlyCollection<Product> Products { get; }

    IReadOnlyCollection<Order> Orders { get; }

    void AddProduct(Product product);

    void AddOrder(Order order);
}

