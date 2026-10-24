using ShopSystem.Application.Catalog;
using ShopSystem.Core.Catalog;

namespace ShopSystem.Application.Tests;

public sealed class ProductServiceTests
{
    [Fact]
    public void AddProduct_WhenPriceIsNegative_ThrowsArgumentException()
    {
        var service = new ProductService(new FakeShopRepository());
        var product = new Product { Id = 1, Name = "Товар", Price = -10m };

        var exception = Assert.Throws<ArgumentException>(() => service.AddProduct(product));

        Assert.Contains("Цена", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddProduct_WhenIdAlreadyExists_ThrowsInvalidOperationException()
    {
        var repository = new FakeShopRepository();
        var service = new ProductService(repository);
        service.AddProduct(new Product { Id = 1, Name = "Клавиатура", Price = 1500m });

        Assert.Throws<InvalidOperationException>(() =>
            service.AddProduct(new Product { Id = 1, Name = "Мышь", Price = 800m }));
    }
}
