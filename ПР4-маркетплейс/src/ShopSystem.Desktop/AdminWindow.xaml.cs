using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;

namespace ShopSystem.Desktop;

public partial class AdminWindow : Window
{
    private readonly HttpClient _http;
    private AdminProduct? _selectedProduct;
    private AdminCategory? _selectedCategory;
    private AdminPickupPoint? _selectedPickup;
    private AdminOrder? _selectedOrder;

    public AdminWindow(HttpClient http)
    {
        InitializeComponent();
        _http = http;
        Loaded += async (_, _) => await RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        await RefreshProductsAsync();
        await RefreshCategoriesAsync();
        await RefreshPickupAsync();
        await RefreshOrdersAsync();
        await RefreshReviewsAsync();
    }

    private async Task RefreshProductsAsync() => await LoadGridAsync<AdminProduct>("/api/admin/products", ProductsGrid);
    private async Task RefreshCategoriesAsync() => await LoadGridAsync<AdminCategory>("/api/categories", CategoriesGrid);
    private async Task RefreshPickupAsync() => await LoadGridAsync<AdminPickupPoint>("/api/admin/pickup-points", PickupGrid);
    private async Task RefreshOrdersAsync() => await LoadGridAsync<AdminOrder>("/api/admin/orders", OrdersGrid);
    private async Task RefreshReviewsAsync() => await LoadGridAsync<AdminReview>("/api/admin/reviews", ReviewsGrid);

    private async Task LoadGridAsync<T>(string path, DataGrid grid)
    {
        try { grid.ItemsSource = await _http.GetFromJsonAsync<List<T>>(path) ?? []; StatusText.Text = "Данные обновлены."; }
        catch (Exception ex) { StatusText.Text = "Не удалось загрузить данные: " + ex.Message; }
    }

    private async Task SendAsync(HttpMethod method, string path, object? body = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                StatusText.Text = $"Ошибка {(int)response.StatusCode}: {detail}";
                return;
            }
            StatusText.Text = "Изменения сохранены.";
        }
        catch (Exception ex) { StatusText.Text = "Не удалось выполнить запрос: " + ex.Message; }
    }

    private void ProductSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedProduct = ProductsGrid.SelectedItem as AdminProduct;
        if (_selectedProduct is not { } p) return;
        ProductNameBox.Text = p.Name; ProductDescriptionBox.Text = p.Description; ProductPriceBox.Text = p.Price.ToString(CultureInfo.InvariantCulture);
        ProductImageBox.Text = p.ImagePath; ProductStockBox.Text = p.Stock.ToString(CultureInfo.InvariantCulture);
        ProductCategoryIdBox.Text = p.CategoryId.ToString(); ProductAvailableBox.IsChecked = p.IsAvailable;
    }

    private bool TryReadProduct(out object payload)
    {
        payload = new { };
        if (!decimal.TryParse(ProductPriceBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ||
            !int.TryParse(ProductStockBox.Text, out var stock) || !int.TryParse(ProductCategoryIdBox.Text, out var categoryId))
        { StatusText.Text = "Проверьте цену, остаток и ID категории."; return false; }
        payload = new { Name = ProductNameBox.Text, Description = ProductDescriptionBox.Text, Price = price, ImagePath = ProductImageBox.Text, Stock = stock, CategoryId = categoryId };
        return true;
    }

    private async void CreateProduct_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadProduct(out var payload)) return;
        await SendAsync(HttpMethod.Post, "/api/admin/products", payload);
        await RefreshProductsAsync();
    }

    private async void UpdateProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct is null) { StatusText.Text = "Выберите товар."; return; }
        if (!TryReadProduct(out var payload)) return;
        await SendAsync(HttpMethod.Put, $"/api/admin/products/{_selectedProduct.Id}", payload);
        await RefreshProductsAsync();
    }

    private async void ToggleProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct is null) { StatusText.Text = "Выберите товар."; return; }
        var value = !_selectedProduct.IsAvailable;
        await SendAsync(HttpMethod.Patch, $"/api/admin/products/{_selectedProduct.Id}/availability", new { IsAvailable = value });
        await RefreshProductsAsync();
    }

    private async void RefreshProducts_Click(object sender, RoutedEventArgs e) => await RefreshProductsAsync();

    private void CategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedCategory = CategoriesGrid.SelectedItem as AdminCategory;
        if (_selectedCategory is not null) CategoryNameBox.Text = _selectedCategory.Name;
    }

    private async void CreateCategory_Click(object sender, RoutedEventArgs e)
    {
        await SendAsync(HttpMethod.Post, "/api/admin/categories", new { Name = CategoryNameBox.Text });
        await RefreshCategoriesAsync();
    }

    private async void UpdateCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCategory is null) { StatusText.Text = "Выберите категорию."; return; }
        await SendAsync(HttpMethod.Put, $"/api/admin/categories/{_selectedCategory.Id}", new { Name = CategoryNameBox.Text });
        await RefreshCategoriesAsync();
    }

    private async void RefreshCategories_Click(object sender, RoutedEventArgs e) => await RefreshCategoriesAsync();

    private void PickupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPickup = PickupGrid.SelectedItem as AdminPickupPoint;
        if (_selectedPickup is not null) { PickupNameBox.Text = _selectedPickup.Name; PickupAddressBox.Text = _selectedPickup.Address; PickupActiveBox.IsChecked = _selectedPickup.IsActive; }
    }

    private async void CreatePickup_Click(object sender, RoutedEventArgs e)
    {
        await SendAsync(HttpMethod.Post, "/api/admin/pickup-points", new { Name = PickupNameBox.Text, Address = PickupAddressBox.Text, IsActive = PickupActiveBox.IsChecked == true });
        await RefreshPickupAsync();
    }

    private async void UpdatePickup_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPickup is null) { StatusText.Text = "Выберите пункт выдачи."; return; }
        await SendAsync(HttpMethod.Put, $"/api/admin/pickup-points/{_selectedPickup.Id}", new { Name = PickupNameBox.Text, Address = PickupAddressBox.Text, IsActive = PickupActiveBox.IsChecked == true });
        await RefreshPickupAsync();
    }

    private async void RefreshPickup_Click(object sender, RoutedEventArgs e) => await RefreshPickupAsync();

    private void OrderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOrder = OrdersGrid.SelectedItem as AdminOrder;
        if (_selectedOrder is null) return;
        foreach (ComboBoxItem item in OrderStatusBox.Items)
            if (string.Equals(item.Content?.ToString(), _selectedOrder.Status, StringComparison.OrdinalIgnoreCase)) { OrderStatusBox.SelectedItem = item; break; }
    }

    private async void UpdateOrderStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder is null) { StatusText.Text = "Выберите заказ."; return; }
        var status = (OrderStatusBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrWhiteSpace(status)) { StatusText.Text = "Выберите статус."; return; }
        await SendAsync(HttpMethod.Put, $"/api/admin/orders/{_selectedOrder.Id}/status", new { Status = status });
        await RefreshOrdersAsync();
    }

    private async void RefreshOrders_Click(object sender, RoutedEventArgs e) => await RefreshOrdersAsync();

    private async void DeleteReview_Click(object sender, RoutedEventArgs e)
    {
        if (ReviewsGrid.SelectedItem is not AdminReview review) { StatusText.Text = "Выберите отзыв."; return; }
        var answer = MessageBox.Show(this, $"Удалить отзыв пользователя {review.UserName} к товару «{review.Product}»?", "Удаление отзыва", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        await SendAsync(HttpMethod.Delete, $"/api/admin/reviews/{review.Id}");
        await RefreshReviewsAsync();
    }

    private async void RefreshReviews_Click(object sender, RoutedEventArgs e) => await RefreshReviewsAsync();

    private sealed record AdminProduct(int Id, string Name, string Description, decimal Price, string ImagePath, decimal Rating, int Stock, bool IsAvailable, int CategoryId, string Category);
    private sealed record AdminCategory(int Id, string Name);
    private sealed record AdminPickupPoint(int Id, string Name, string Address, bool IsActive);
    private sealed record AdminOrder(int Id, string Customer, DateTime CreatedAtUtc, decimal Total, string Status);
    private sealed record AdminReview(int Id, int ProductId, string Product, string UserName, int Rating, string Text, DateTime CreatedAtUtc);
}
