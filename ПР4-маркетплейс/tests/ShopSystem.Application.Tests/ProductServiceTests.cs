using ShopSystem.Application.Catalog;
using ShopSystem.Core.Catalog;

namespace ShopSystem.Application.Tests;

public sealed class ProductServiceTests
{
    [Fact]
    public void AddProduct_WhenPriceIsNegative_ThrowsArgumentException()
    {
        var service = new ProductService(new FakeShopRepository());
        var product = new Product { Id = 1, Name = "Broken product", Price = -1m };

        var exception = Assert.Throws<ArgumentException>(() => service.AddProduct(product));

        Assert.Contains("price", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddProduct_WhenIdAlreadyExists_ThrowsInvalidOperationException()
    {
        var repository = new FakeShopRepository();
        var service = new ProductService(repository);
        service.AddProduct(new Product { Id = 1, Name = "Keyboard", Price = 100m });

        Assert.Throws<InvalidOperationException>(() =>
            service.AddProduct(new Product { Id = 1, Name = "Mouse", Price = 50m }));
    }
}

