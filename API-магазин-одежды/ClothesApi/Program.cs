using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var dbPath = builder.Configuration["CLOTHES_DB"] ?? "clothes.db";

builder.Services.AddDbContext<ClothesDb>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<IPasswordHasher<Customer>, PasswordHasher<Customer>>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "ClothesApi.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Events.OnRedirectToLogin = c =>
    {
        c.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = c =>
    {
        c.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClothesDb>();
    await db.Database.EnsureCreatedAsync();

    if (!await db.Clothes.AnyAsync())
    {
        var shirts = new Clothing
        {
            Name = "Футболка базовая",
            Price = 1200,
            Description = "Хлопковая футболка",
            Purchases = 0,
            Type = "Футболка",
            IsAvailable = true
        };
        var hoodie = new Clothing
        {
            Name = "Худи",
            Price = 3500,
            Description = "Худи с капюшоном",
            Purchases = 0,
            Type = "Толстовка",
            IsAvailable = true
        };

        db.Clothes.AddRange(shirts, hoodie);
        await db.SaveChangesAsync();

        db.Sizes.AddRange(
            new SizeStock { ClothingId = shirts.Id, Label = "S", Number = 44, Stock = 10 },
            new SizeStock { ClothingId = shirts.Id, Label = "M", Number = 46, Stock = 15 },
            new SizeStock { ClothingId = shirts.Id, Label = "L", Number = 48, Stock = 9 },
            new SizeStock { ClothingId = hoodie.Id, Label = "M", Number = 46, Stock = 4 }
        );
        await db.SaveChangesAsync();
    }

    var managerPassword = app.Configuration["CLOTHES_MANAGER_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(managerPassword) && !await db.Customers.AnyAsync(x => x.Role == "Manager"))
    {
        var manager = new Customer
        {
            Login = app.Configuration["CLOTHES_MANAGER_LOGIN"] ?? "manager",
            Phone = "",
            Address = "",
            Role = "Manager"
        };
        manager.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Customer>>().HashPassword(manager, managerPassword);
        db.Customers.Add(manager);
        await db.SaveChangesAsync();
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/clothing", async (ClothesDb db, string? search) =>
{
    var items = await db.Clothes.AsNoTracking().Include(x => x.Sizes).OrderBy(x => x.Name).ToListAsync();
    if (!string.IsNullOrWhiteSpace(search))
    {
        items = items.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    return Results.Ok(items.Select(x => new
    {
        x.Id,
        x.Name,
        x.Price,
        x.Description,
        x.Purchases,
        x.Type,
        x.IsAvailable,
        Sizes = x.Sizes.Select(s => new { s.Label, s.Number, s.Stock })
    }).ToList());
});

app.MapPost("/api/users/register", async (RegisterDto dto, ClothesDb db, IPasswordHasher<Customer> hasher) =>
{
    if (string.IsNullOrWhiteSpace(dto.Login) || dto.Password.Length < 8 || string.IsNullOrWhiteSpace(dto.Phone))
    {
        return Results.BadRequest(new { error = "Укажите логин и телефон; пароль должен быть не короче 8 символов." });
    }

    var login = dto.Login.Trim();
    if (await db.Customers.AnyAsync(x => x.Login == login))
    {
        return Results.Conflict(new { error = "Логин уже занят." });
    }

    var user = new Customer
    {
        Login = login,
        Phone = dto.Phone.Trim(),
        Address = dto.Address?.Trim() ?? "",
        Role = "Customer"
    };
    user.PasswordHash = hasher.HashPassword(user, dto.Password);
    db.Customers.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Login, user.Role });
});

app.MapPost("/api/auth/login", async (LoginDto dto, ClothesDb db, IPasswordHasher<Customer> hasher, HttpContext ctx) =>
{
    var user = await db.Customers.SingleOrDefaultAsync(x => x.Login == dto.Login.Trim());
    if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Login),
        new Claim(ClaimTypes.Role, user.Role)
    };
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

    return Results.Ok(new { user.Id, user.Login, user.Role });
});

app.MapPost("/api/auth/logout", async (HttpContext c) =>
{
    await c.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/api/orders", async (OrderDto dto, ClaimsPrincipal principal, ClothesDb db) =>
{
    var uid = UserId(principal);
    var customer = await db.Customers.FindAsync(uid);
    if (customer is null)
    {
        return Results.Unauthorized();
    }

    if (dto.Items.Count == 0)
    {
        return Results.BadRequest(new { error = "Заказ должен содержать товары." });
    }

    await using var tx = await db.Database.BeginTransactionAsync();
    var order = new CustomerOrder
    {
        CreatedAt = DateTime.UtcNow,
        Status = "New",
        CustomerId = uid
    };

    foreach (var item in dto.Items)
    {
        if (item.Quantity < 1)
        {
            return Results.BadRequest(new { error = "Количество должно быть положительным." });
        }

        var stock = await db.Sizes.SingleOrDefaultAsync(s => s.Id == item.SizeId);
        if (stock is null || stock.Stock < item.Quantity || !await db.Clothes.AnyAsync(c => c.Id == stock.ClothingId && c.IsAvailable))
        {
            return Results.BadRequest(new { error = "Товар/размер недоступен или недостаточно остатка." });
        }

        var clothing = await db.Clothes.FindAsync(stock.ClothingId);
        if (clothing is null)
        {
            return Results.BadRequest();
        }

        stock.Stock -= item.Quantity;
        clothing.Purchases += item.Quantity;
        order.Lines.Add(new OrderLine
        {
            ClothingId = clothing.Id,
            SizeId = stock.Id,
            Quantity = item.Quantity,
            UnitPrice = clothing.Price,
            ClothingName = clothing.Name,
            SizeLabel = stock.Label
        });
    }

    db.Orders.Add(order);
    await db.SaveChangesAsync();
    await tx.CommitAsync();

    return Results.Created($"/api/orders/{order.Id}", new { order.Id, order.CreatedAt, order.Status });
}).RequireAuthorization();

app.MapGet("/api/orders", async (ClaimsPrincipal u, ClothesDb db, string? status, int? customerId) =>
{
    var uid = UserId(u);
    var q = db.Orders.AsNoTracking().Include(o => o.Lines).AsQueryable();

    if (!u.IsInRole("Manager"))
    {
        q = q.Where(o => o.CustomerId == uid);
    }
    else if (customerId.HasValue)
    {
        q = q.Where(o => o.CustomerId == customerId.Value);
    }

    if (!string.IsNullOrWhiteSpace(status))
    {
        q = q.Where(o => o.Status == status);
    }

    return Results.Ok(await q.OrderByDescending(o => o.CreatedAt).Select(o => new
    {
        o.Id,
        o.CreatedAt,
        o.Status,
        o.CustomerId,
        Items = o.Lines.Select(l => new { l.ClothingName, l.SizeLabel, l.Quantity, l.UnitPrice })
    }).ToListAsync());
}).RequireAuthorization();

app.MapPatch("/api/manager/orders/{id:int}/status", async (int id, StatusDto dto, ClothesDb db) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null)
    {
        return Results.NotFound();
    }

    order.Status = dto.Status.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(new { order.Id, order.Status });
}).RequireAuthorization(p => p.RequireRole("Manager"));

app.MapPatch("/api/manager/sizes/{id:int}/stock", async (int id, StockDto dto, ClothesDb db) =>
{
    var size = await db.Sizes.FindAsync(id);
    if (size is null)
    {
        return Results.NotFound();
    }

    if (dto.Stock < 0)
    {
        return Results.BadRequest();
    }

    size.Stock = dto.Stock;
    await db.SaveChangesAsync();
    return Results.Ok(new { size.Id, size.Stock });
}).RequireAuthorization(p => p.RequireRole("Manager"));

app.MapPatch("/api/manager/clothing/{id:int}/availability", async (int id, AvailabilityDto dto, ClothesDb db) =>
{
    var item = await db.Clothes.FindAsync(id);
    if (item is null)
    {
        return Results.NotFound();
    }

    item.IsAvailable = dto.IsAvailable;
    await db.SaveChangesAsync();
    return Results.Ok(new { item.Id, item.IsAvailable });
}).RequireAuthorization(p => p.RequireRole("Manager"));

app.Run();

static int UserId(ClaimsPrincipal u) => int.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);

// DTOs
public record RegisterDto(string Login, string Password, string Phone, string? Address);
public record LoginDto(string Login, string Password);
public record OrderDto(List<OrderItemDto> Items);
public record OrderItemDto(int SizeId, int Quantity);
public record StatusDto(string Status);
public record StockDto(int Stock);
public record AvailabilityDto(bool IsAvailable);

// Entities
public class Customer
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";
    public List<CustomerOrder> Orders { get; set; } = [];
}

public class Clothing
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Purchases { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public List<SizeStock> Sizes { get; set; } = [];
}

public class SizeStock
{
    public int Id { get; set; }
    public int ClothingId { get; set; }
    public Clothing? Clothing { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Number { get; set; }
    public int Stock { get; set; }
}

public class CustomerOrder
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "New";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

public class OrderLine
{
    public int Id { get; set; }
    public int CustomerOrderId { get; set; }
    public CustomerOrder? Order { get; set; }
    public int ClothingId { get; set; }
    public int SizeId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string ClothingName { get; set; } = string.Empty;
    public string SizeLabel { get; set; } = string.Empty;
}

public class ClothesDb(DbContextOptions<ClothesDb> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Clothing> Clothes => Set<Clothing>();
    public DbSet<SizeStock> Sizes => Set<SizeStock>();
    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>().HasIndex(x => x.Login).IsUnique();
        b.Entity<Clothing>().Property(x => x.Price).HasPrecision(10, 2);
        b.Entity<OrderLine>().Property(x => x.UnitPrice).HasPrecision(10, 2);

        b.Entity<CustomerOrder>()
            .HasOne(x => x.Customer)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerId);

        b.Entity<SizeStock>()
            .HasOne(x => x.Clothing)
            .WithMany(x => x.Sizes)
            .HasForeignKey(x => x.ClothingId);

        b.Entity<OrderLine>()
            .HasOne(x => x.Order)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.CustomerOrderId);
    }
}
