using ShopSystem.Application.Orders;
using ShopSystem.Core.Catalog;

namespace ShopSystem.Application.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public void CreateOrder_WithKnownProducts_CalculatesTotalAndSavesOrder()
    {
        var repository = new FakeShopRepository();
        repository.AddProduct(new Product { Id = 1, Name = "Keyboard", Price = 120m });
        repository.AddProduct(new Product { Id = 2, Name = "Mouse", Price = 50m });
        var service = new OrderService(repository);

        var order = service.CreateOrder("  Ivan Petrov  ", [1, 2]);

        Assert.Equal("Ivan Petrov", order.CustomerName);
        Assert.Equal(170m, order.TotalAmount);
        Assert.Single(repository.Orders);
    }

    [Fact]
    public void CreateOrder_WithoutKnownProducts_ThrowsInvalidOperationException()
    {
        var service = new OrderService(new FakeShopRepository());

        Assert.Throws<InvalidOperationException>(() => service.CreateOrder("Ivan", [999]));
    }
}

