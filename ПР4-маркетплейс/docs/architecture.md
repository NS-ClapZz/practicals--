# Архитектура ShopSystem

## Организация решения

Решение разделено на доменную модель, прикладные сервисы, инфраструктуру, REST API и десктопный клиент на WPF. Для основного сценария API использует EF Core и базу данных SQLite; также сохранена консольная версия для автономной демонстрации логики.

```text
ShopSystem.Desktop ──HTTP──> ShopSystem.Api ──> SQLite
ShopSystem.ConsoleApp ──> ShopSystem.Application ──> ShopSystem.Core
          │                         ▲
          └──> ShopSystem.Infrastructure ──────────┘
```

- **`ShopSystem.Core`** — доменные сущности каталога товаров и заказов. Не имеет внешних зависимостей.
- **`ShopSystem.Application`** — бизнес-логика и сценарии работы с товарами и заказами, интерфейс репозитория `IShopRepository`. Зависит только от `ShopSystem.Core`.
- **`ShopSystem.Infrastructure`** — реализация хранилища данных в памяти (`InMemoryShopRepository`).
- **`ShopSystem.ConsoleApp`** — консольное приложение, демонстрирующее работу со слоями ядра и сервисов без веб-интерфейса.
- **`ShopSystem.Application.Tests`** — модульные xUnit-тесты для проверки сценариев приложения.
- **`ShopSystem.Api`** — веб-сервис на ASP.NET Core: эндпоинты каталога, корзины, заказов, отзывов и панели администратора.
- **`ShopSystem.Desktop`** — клиентское приложение на WPF: каталог с фильтрацией, корзина, оформление заказов, избранное и модерация.

## Структура компонентов

| Исходный компонент | Расположение | Назначение |
|---|---|---|
| `Program.cs` | `src/ShopSystem.ConsoleApp/Program.cs` | Точка входа консольного приложения |
| `Product.cs` | `src/ShopSystem.Core/Catalog/Product.cs` | Доменная модель товара |
| `Order.cs` | `src/ShopSystem.Core/Orders/Order.cs` | Доменная модель заказа |
| `ProductService.cs` | `src/ShopSystem.Application/Catalog/ProductService.cs` | Сервис управления каталогом |
| `OrderService.cs` | `src/ShopSystem.Application/Orders/OrderService.cs` | Сервис оформления заказов |
| `Database.cs` | `src/ShopSystem.Infrastructure/Persistence/InMemoryShopRepository.cs` | Реализация репозитория в памяти |
| `Tests.cs` | `tests/ShopSystem.Application.Tests/*Tests.cs` | Модульные xUnit-тесты |
| `appsettings.json` | `src/ShopSystem.ConsoleApp/appsettings.example.json` | Шаблон конфигурации |
| `architecture.png` | `docs/architecture.png` | Архитектурная схема компонентов |
