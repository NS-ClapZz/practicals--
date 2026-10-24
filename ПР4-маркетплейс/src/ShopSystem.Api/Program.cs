using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopSystem.Api.Domain;
using ShopSystem.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var databasePath = builder.Configuration["SHOP_DB"] ?? "shop.db";
builder.Services.AddDbContext<ShopDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "ShopSystem.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Categories.AnyAsync())
    {
        var categories = new[] { new Category { Name = "Электроника" }, new Category { Name = "Аксессуары" }, new Category { Name = "Дом" } };
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();
        for (var i = 1; i <= 15; i++)
            db.Products.Add(new Product { Name = $"Товар {i}", Description = $"Демонстрационный товар каталога {i}", Price = 100m * i, ImagePath = "images/placeholder.svg", Stock = 5 + i, CategoryId = categories[(i - 1) % categories.Length].Id });
        db.PickupPoints.AddRange(new PickupPoint { Name = "ПВЗ Центр", Address = "Москва, ул. Примерная, 1" }, new PickupPoint { Name = "ПВЗ Север", Address = "Москва, ул. Учебная, 10" });
        await db.SaveChangesAsync();
    }
    var adminName = app.Configuration["SHOP_ADMIN_USER"] ?? "admin";
    var adminPassword = app.Configuration["SHOP_ADMIN_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(adminPassword) && !await db.Users.AnyAsync(x => x.Role == "Admin"))
    {
        var admin = new AppUser { UserName = adminName, Role = "Admin" };
        admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>().HashPassword(admin, adminPassword);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/categories", async (ShopDbContext db) => Results.Ok(await db.Categories.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync()));
app.MapGet("/api/products", async (ShopDbContext db, string? search, int? categoryId, decimal? minPrice, decimal? maxPrice) =>
{
    var query = db.Products.AsNoTracking().Include(x => x.Category).Where(x => x.IsAvailable && x.Stock > 0);
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Description.Contains(search));
    if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
    if (minPrice.HasValue) query = query.Where(x => x.Price >= minPrice.Value);
    if (maxPrice.HasValue) query = query.Where(x => x.Price <= maxPrice.Value);
    return Results.Ok(await query.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Description, x.Price, x.ImagePath, Rating = db.Reviews.Where(r => r.ProductId == x.Id).Average(r => (double?)r.Rating) ?? 0d, x.Stock, Category = x.Category!.Name }).ToListAsync());
});

app.MapGet("/api/products/{productId:int}/reviews", async (int productId, ShopDbContext db) =>
{
    if (!await db.Products.AnyAsync(x => x.Id == productId)) return Results.NotFound();
    var reviews = await db.Reviews.AsNoTracking().Where(x => x.ProductId == productId).OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => new { x.Id, x.Rating, x.Text, x.CreatedAtUtc, UserName = x.User!.UserName }).ToListAsync();
    var average = reviews.Count == 0 ? 0m : Math.Round((decimal)reviews.Average(x => x.Rating), 2);
    return Results.Ok(new { AverageRating = average, Count = reviews.Count, Items = reviews });
});
app.MapPost("/api/products/{productId:int}/reviews", async (int productId, ReviewRequest request, ClaimsPrincipal principal, ShopDbContext db) =>
{
    if (request.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > 1000)
        return Results.BadRequest(new { error = "Оценка должна быть от 1 до 5, текст — от 1 до 1000 символов." });
    if (!await db.Products.AnyAsync(x => x.Id == productId)) return Results.NotFound(new { error = "Товар не найден." });
    var userId = UserId(principal);
    if (await db.Reviews.AnyAsync(x => x.UserId == userId && x.ProductId == productId))
        return Results.Conflict(new { error = "Вы уже оставили отзыв на этот товар. Его можно изменить." });
    var review = new ProductReview { UserId = userId, ProductId = productId, Rating = request.Rating, Text = request.Text.Trim(), CreatedAtUtc = DateTime.UtcNow };
    db.Reviews.Add(review); await db.SaveChangesAsync();
    return Results.Created($"/api/products/{productId}/reviews", new { review.Id, review.Rating, review.Text, review.CreatedAtUtc });
}).RequireAuthorization();
app.MapPut("/api/products/{productId:int}/reviews", async (int productId, ReviewRequest request, ClaimsPrincipal principal, ShopDbContext db) =>
{
    if (request.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > 1000)
        return Results.BadRequest(new { error = "Оценка должна быть от 1 до 5, текст — от 1 до 1000 символов." });
    var userId = UserId(principal);
    var review = await db.Reviews.SingleOrDefaultAsync(x => x.ProductId == productId && x.UserId == userId);
    if (review is null) return Results.NotFound();
    review.Rating = request.Rating; review.Text = request.Text.Trim(); review.CreatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/api/favorites", async (ClaimsPrincipal principal, ShopDbContext db) =>
{
    var userId = UserId(principal);
    return Results.Ok(await db.Favorites.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.AddedAtUtc)
        .Select(x => new { x.ProductId, Name = x.Product!.Name, x.Product.Description, x.Product.Price, x.Product.ImagePath, x.Product.Stock, Category = x.Product.Category!.Name, Rating = db.Reviews.Where(r => r.ProductId == x.ProductId).Average(r => (double?)r.Rating) ?? 0d }).ToListAsync());
}).RequireAuthorization();
app.MapPost("/api/favorites/{productId:int}", async (int productId, ClaimsPrincipal principal, ShopDbContext db) =>
{
    if (!await db.Products.AnyAsync(x => x.Id == productId)) return Results.NotFound();
    var userId = UserId(principal);
    if (await db.Favorites.AnyAsync(x => x.UserId == userId && x.ProductId == productId)) return Results.Conflict(new { error = "Товар уже в избранном." });
    db.Favorites.Add(new Favorite { UserId = userId, ProductId = productId, AddedAtUtc = DateTime.UtcNow });
    await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization();
app.MapDelete("/api/favorites/{productId:int}", async (int productId, ClaimsPrincipal principal, ShopDbContext db) =>
{
    var userId = UserId(principal);
    var favorite = await db.Favorites.SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId);
    if (favorite is null) return Results.NotFound();
    db.Favorites.Remove(favorite); await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/api/auth/register", async (RegisterRequest request, ShopDbContext db, IPasswordHasher<AppUser> hasher) =>
{
    var name = request.UserName.Trim();
    if (name.Length < 3 || request.Password.Length < 8) return Results.BadRequest(new { error = "Логин: от 3 символов, пароль: от 8 символов." });
    if (request.Password != request.PasswordRepeat) return Results.BadRequest(new { error = "Пароли не совпадают." });
    if (await db.Users.AnyAsync(x => x.UserName == name)) return Results.Conflict(new { error = "Пользователь уже существует." });
    var user = new AppUser { UserName = name, Role = "Customer" };
    user.PasswordHash = hasher.HashPassword(user, request.Password);
    db.Users.Add(user); await db.SaveChangesAsync();
    return Results.Created("/api/auth/me", new { user.Id, user.UserName });
});
app.MapPost("/api/auth/login", async (LoginRequest request, ShopDbContext db, IPasswordHasher<AppUser> hasher, HttpContext context) =>
{
    var user = await db.Users.SingleOrDefaultAsync(x => x.UserName == request.UserName.Trim());
    if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();
    var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.UserName), new Claim(ClaimTypes.Role, user.Role) };
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Ok(new { user.Id, user.UserName, user.Role });
});
app.MapPost("/api/auth/logout", async (HttpContext context) => { await context.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
app.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(new { user = user.Identity!.Name, role = user.FindFirstValue(ClaimTypes.Role) })).RequireAuthorization();

app.MapGet("/api/pickup-points", async (ShopDbContext db) => Results.Ok(await db.PickupPoints.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Address }).ToListAsync()));
app.MapGet("/api/cart", async (ClaimsPrincipal principal, ShopDbContext db) =>
{
    var userId = UserId(principal);
    return Results.Ok(await db.CartItems.AsNoTracking().Where(x => x.UserId == userId).Include(x => x.Product).Select(x => new { x.ProductId, x.Product!.Name, x.Product.ImagePath, x.Product.Price, x.Quantity, LineTotal = x.Product.Price * x.Quantity }).ToListAsync());
}).RequireAuthorization();
app.MapPost("/api/cart", async (CartRequest request, ClaimsPrincipal principal, ShopDbContext db) =>
{
    if (request.Quantity < 1) return Results.BadRequest(new { error = "Количество должно быть положительным." });
    var userId = UserId(principal); var product = await db.Products.SingleOrDefaultAsync(x => x.Id == request.ProductId && x.IsAvailable);
    if (product is null) return Results.NotFound(new { error = "Товар недоступен." });
    var item = await db.CartItems.SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == request.ProductId);
    var quantity = (item?.Quantity ?? 0) + request.Quantity;
    if (quantity > product.Stock) return Results.Conflict(new { error = "Недостаточно товара на складе." });
    if (item is null) db.CartItems.Add(new CartItem { UserId = userId, ProductId = product.Id, Quantity = quantity }); else item.Quantity = quantity;
    await db.SaveChangesAsync(); return Results.Ok(new { product.Id, Quantity = quantity });
}).RequireAuthorization();
app.MapPut("/api/cart/{productId:int}", async (int productId, CartRequest request, ClaimsPrincipal principal, ShopDbContext db) =>
{
    var userId = UserId(principal);
    var item = await db.CartItems.Include(x => x.Product).SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId);
    if (item is null) return Results.NotFound();
    if (request.Quantity <= 0) { db.CartItems.Remove(item); await db.SaveChangesAsync(); return Results.NoContent(); }
    if (request.Quantity > item.Product!.Stock) return Results.Conflict(new { error = "Недостаточно товара на складе." });
    item.Quantity = request.Quantity; await db.SaveChangesAsync(); return Results.Ok(new { productId, item.Quantity });
}).RequireAuthorization();
app.MapDelete("/api/cart/{productId:int}", async (int productId, ClaimsPrincipal principal, ShopDbContext db) =>
{
    var userId = UserId(principal);
    var item = await db.CartItems.SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId);
    if (item is null) return Results.NotFound(); db.CartItems.Remove(item); await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/api/orders", async (CheckoutRequest request, ClaimsPrincipal principal, ShopDbContext db) => await Checkout(request, UserId(principal), db)).RequireAuthorization();
app.MapPost("/api/orders/buy/{productId:int}", async (int productId, BuyRequest request, ClaimsPrincipal principal, ShopDbContext db) =>
{
    var userId = UserId(principal); var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId && x.IsAvailable && x.Stock > 0);
    if (product is null) return Results.NotFound(new { error = "Товар недоступен." });
    string destination;
    if (request.Fulfillment == FulfillmentType.AddressDelivery)
    {
        if (product.Price <= 600) return Results.BadRequest(new { error = "Доставка по адресу доступна при сумме заказа больше 600 ₽." });
        if (string.IsNullOrWhiteSpace(request.Address)) return Results.BadRequest(new { error = "Укажите адрес доставки." });
        destination = request.Address.Trim();
    }
    else
    {
        var point = await db.PickupPoints.SingleOrDefaultAsync(x => x.Id == request.PickupPointId && x.IsActive);
        if (point is null) return Results.BadRequest(new { error = "Выберите действующий пункт выдачи." });
        destination = point.Name + ", " + point.Address;
    }
    await using var transaction = await db.Database.BeginTransactionAsync();
    var order = new Order { UserId = userId, CreatedAtUtc = DateTime.UtcNow, Fulfillment = request.Fulfillment, Destination = destination, Total = product.Price };
    order.Items.Add(new OrderItem { ProductId = product.Id, ProductNameAtPurchase = product.Name, UnitPriceAtPurchase = product.Price, Quantity = 1 });
    product.Stock--;
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.Created($"/api/orders/{order.Id}", new { order.Id, order.Total, order.Status, order.Destination });
}).RequireAuthorization();
app.MapGet("/api/orders", async (ClaimsPrincipal principal, ShopDbContext db, string? sort) =>
{
    var userId = UserId(principal);
    var query = db.Orders.AsNoTracking().Where(x => x.UserId == userId);
    query = sort == "oldest" ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc);
    return Results.Ok(await query.Select(x => new { x.Id, x.CreatedAtUtc, x.Total, x.Status, x.Fulfillment, x.Destination }).ToListAsync());
}).RequireAuthorization();
app.MapGet("/api/orders/{id:int}", async (int id, ClaimsPrincipal principal, ShopDbContext db) =>
{
    var userId = UserId(principal);
    var order = await db.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    return order is null ? Results.NotFound() : Results.Ok(new { order.Id, order.CreatedAtUtc, order.Total, order.Status, order.Fulfillment, order.Destination, Items = order.Items.Select(i => new { i.ProductId, i.ProductNameAtPurchase, i.UnitPriceAtPurchase, i.Quantity, LineTotal = i.UnitPriceAtPurchase * i.Quantity }) });
}).RequireAuthorization();

app.MapPost("/api/admin/products", async (ProductRequest request, ShopDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0 || request.Stock < 0 || !await db.Categories.AnyAsync(x => x.Id == request.CategoryId)) return Results.BadRequest(new { error = "Проверьте поля товара и категорию." });
    var product = new Product { Name = request.Name.Trim(), Description = request.Description.Trim(), Price = request.Price, ImagePath = SafeImage(request.ImagePath), Stock = request.Stock, IsAvailable = true, CategoryId = request.CategoryId };
    db.Products.Add(product); await db.SaveChangesAsync(); return Results.Created($"/api/products/{product.Id}", new { product.Id, product.Name });
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapGet("/api/admin/products", async (ShopDbContext db) => Results.Ok(await db.Products.AsNoTracking().Include(x => x.Category).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Description, x.Price, x.ImagePath, Rating = db.Reviews.Where(r => r.ProductId == x.Id).Average(r => (double?)r.Rating) ?? 0d, x.Stock, x.IsAvailable, x.CategoryId, Category = x.Category!.Name }).ToListAsync())).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapPut("/api/admin/products/{id:int}", async (int id, ProductRequest request, ShopDbContext db) =>
{
    var product = await db.Products.FindAsync(id); if (product is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0 || request.Stock < 0 || !await db.Categories.AnyAsync(x => x.Id == request.CategoryId)) return Results.BadRequest();
    product.Name = request.Name.Trim(); product.Description = request.Description.Trim(); product.Price = request.Price; product.ImagePath = SafeImage(request.ImagePath); product.Stock = request.Stock; product.CategoryId = request.CategoryId;
    await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapPatch("/api/admin/products/{id:int}/availability", async (int id, AvailabilityRequest request, ShopDbContext db) =>
{
    var product = await db.Products.FindAsync(id); if (product is null) return Results.NotFound(); product.IsAvailable = request.IsAvailable; await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapPost("/api/admin/categories", async (NameRequest request, ShopDbContext db) =>
{
    var name = request.Name.Trim(); if (name.Length == 0) return Results.BadRequest(); if (await db.Categories.AnyAsync(x => x.Name == name)) return Results.Conflict();
    var category = new Category { Name = name }; db.Categories.Add(category); await db.SaveChangesAsync(); return Results.Created($"/api/categories/{category.Id}", new { category.Id, category.Name });
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapPut("/api/admin/categories/{id:int}", async (int id, NameRequest request, ShopDbContext db) =>
{
    var category = await db.Categories.FindAsync(id); if (category is null) return Results.NotFound();
    var name = request.Name.Trim(); if (name.Length == 0) return Results.BadRequest();
    if (await db.Categories.AnyAsync(x => x.Id != id && x.Name == name)) return Results.Conflict();
    category.Name = name; await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapPost("/api/admin/pickup-points", async (PickupRequest request, ShopDbContext db) =>
{
    var point = new PickupPoint { Name = request.Name.Trim(), Address = request.Address.Trim() }; if (point.Name.Length == 0 || point.Address.Length == 0) return Results.BadRequest();
    db.PickupPoints.Add(point); await db.SaveChangesAsync(); return Results.Created($"/api/pickup-points/{point.Id}", new { point.Id, point.Name, point.Address });
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapGet("/api/admin/pickup-points", async (ShopDbContext db) => Results.Ok(await db.PickupPoints.AsNoTracking().OrderBy(x => x.Name).ToListAsync())).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapPut("/api/admin/pickup-points/{id:int}", async (int id, PickupRequest request, ShopDbContext db) =>
{
    var point = await db.PickupPoints.FindAsync(id); if (point is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address)) return Results.BadRequest();
    point.Name = request.Name.Trim(); point.Address = request.Address.Trim(); point.IsActive = request.IsActive;
    await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapPut("/api/admin/orders/{id:int}/status", async (int id, StatusRequest request, ShopDbContext db) =>
{
    var order = await db.Orders.FindAsync(id); if (order is null) return Results.NotFound(); if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status)) return Results.BadRequest(new { error = "Неизвестный статус." });
    order.Status = status; await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapGet("/api/admin/orders", async (ShopDbContext db) => Results.Ok(await db.Orders.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, Customer = x.User!.UserName, x.CreatedAtUtc, x.Total, x.Status }).ToListAsync())).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapGet("/api/admin/reviews", async (ShopDbContext db) => Results.Ok(await db.Reviews.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.ProductId, Product = x.Product!.Name, UserName = x.User!.UserName, x.Rating, x.Text, x.CreatedAtUtc }).ToListAsync())).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.MapDelete("/api/admin/reviews/{id:int}", async (int id, ShopDbContext db) =>
{
    var review = await db.Reviews.FindAsync(id); if (review is null) return Results.NotFound();
    db.Reviews.Remove(review); await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));

app.Run();

static int UserId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
static string SafeImage(string? value) => string.IsNullOrWhiteSpace(value) ? "images/placeholder.svg" : value.Trim().Replace("..", "");
static async Task<IResult> Checkout(CheckoutRequest request, int userId, ShopDbContext db)
{
    var cart = await db.CartItems.Include(x => x.Product).Where(x => x.UserId == userId).ToListAsync();
    if (cart.Count == 0) return Results.BadRequest(new { error = "Корзина пуста." });
    if (cart.Any(x => x.Product is null || !x.Product.IsAvailable || x.Quantity > x.Product.Stock)) return Results.Conflict(new { error = "Проверьте доступность и остатки товаров." });
    var total = cart.Sum(x => x.Product!.Price * x.Quantity);
    string destination;
    if (request.Fulfillment == FulfillmentType.AddressDelivery)
    {
        if (total <= 600) return Results.BadRequest(new { error = "Доставка по адресу доступна при сумме заказа больше 600 ₽." });
        if (string.IsNullOrWhiteSpace(request.Address)) return Results.BadRequest(new { error = "Укажите адрес доставки." });
        destination = request.Address.Trim();
    }
    else
    {
        var point = await db.PickupPoints.SingleOrDefaultAsync(x => x.Id == request.PickupPointId && x.IsActive);
        if (point is null) return Results.BadRequest(new { error = "Выберите действующий пункт выдачи." });
        destination = point.Name + ", " + point.Address;
    }
    await using var transaction = await db.Database.BeginTransactionAsync();
    var order = new Order { UserId = userId, CreatedAtUtc = DateTime.UtcNow, Fulfillment = request.Fulfillment, Destination = destination, Total = total };
    foreach (var item in cart)
    {
        var product = item.Product!; product.Stock -= item.Quantity;
        order.Items.Add(new OrderItem { ProductId = product.Id, ProductNameAtPurchase = product.Name, UnitPriceAtPurchase = product.Price, Quantity = item.Quantity });
    }
    db.Orders.Add(order); db.CartItems.RemoveRange(cart); await db.SaveChangesAsync(); await transaction.CommitAsync();
    return Results.Created($"/api/orders/{order.Id}", new { order.Id, order.Total, order.Status, order.Destination });
}

record RegisterRequest(string UserName, string Password, string PasswordRepeat);
record LoginRequest(string UserName, string Password);
record CartRequest(int ProductId, int Quantity);
record CheckoutRequest(FulfillmentType Fulfillment, string? Address, int? PickupPointId);
record BuyRequest(FulfillmentType Fulfillment, string? Address, int? PickupPointId);
record ProductRequest(string Name, string Description, decimal Price, string? ImagePath, int Stock, int CategoryId);
record ReviewRequest(int Rating, string Text);
record AvailabilityRequest(bool IsAvailable);
record NameRequest(string Name);
record PickupRequest(string Name, string Address, bool IsActive = true);
record StatusRequest(string Status);
