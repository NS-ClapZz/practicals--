using ShopSystem.Application.Orders;
using ShopSystem.Core.Catalog;

namespace ShopSystem.Application.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public void CreateOrder_WithKnownProducts_CalculatesTotalAndSavesOrder()
    {
        var repository = new FakeShopRepository();
        repository.AddProduct(new Product { Id = 1, Name = "Клавиатура", Price = 1200m });
        repository.AddProduct(new Product { Id = 2, Name = "Мышь", Price = 500m });
        var service = new OrderService(repository);

        var order = service.CreateOrder("  Иван Петров  ", [1, 2]);

        Assert.Equal("Иван Петров", order.CustomerName);
        Assert.Equal(1700m, order.TotalAmount);
        Assert.Single(repository.Orders);
    }

    [Fact]
    public void CreateOrder_WithoutKnownProducts_ThrowsInvalidOperationException()
    {
        var service = new OrderService(new FakeShopRepository());

        Assert.Throws<InvalidOperationException>(() => service.CreateOrder("Иван", [999]));
    }
}
