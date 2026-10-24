using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace ShopSystem.Desktop;

public partial class MainWindow : Window
{
    private readonly HttpClient _http;
    private readonly List<ProductView> _cart = [];
    private const string Api = "http://127.0.0.1:5077";

    public MainWindow()
    {
        InitializeComponent();
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
        _http = new HttpClient(handler) { BaseAddress = new Uri(Api) };
        Loaded += async (_, _) => { await LoadCategoriesAsync(); await LoadPickupPointsAsync(); await LoadProductsAsync(null); };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadProductsAsync(SearchBox.Text);
    private async void Search_Click(object sender, RoutedEventArgs e) => await LoadProductsAsync(SearchBox.Text);

    private async Task LoadProductsAsync(string? search)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search));
            if (CategoryBox.SelectedValue is int categoryId) query.Add("categoryId=" + categoryId);
            if (decimal.TryParse(MinPriceBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var min)) query.Add("minPrice=" + min.ToString(CultureInfo.InvariantCulture));
            if (decimal.TryParse(MaxPriceBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var max)) query.Add("maxPrice=" + max.ToString(CultureInfo.InvariantCulture));
            var suffix = query.Count == 0 ? "" : "?" + string.Join("&", query);
            ProductsGrid.ItemsSource = await _http.GetFromJsonAsync<List<ProductView>>("/api/products" + suffix) ?? [];
            StatusText.Text = $"Загружено товаров: {ProductsGrid.Items.Count}";
        }
        catch (Exception ex) { StatusText.Text = "Не удалось подключиться к API: " + ex.Message; }
    }

    private async Task LoadCategoriesAsync()
    {
        try { CategoryBox.ItemsSource = await _http.GetFromJsonAsync<List<CategoryView>>("/api/categories") ?? []; }
        catch (Exception ex) { StatusText.Text = "Не удалось загрузить категории: " + ex.Message; }
    }

    private async Task LoadPickupPointsAsync()
    {
        try
        {
            var selectedId = PickupPointBox.SelectedValue as int?;
            PickupPointBox.ItemsSource = await _http.GetFromJsonAsync<List<PickupPointView>>("/api/pickup-points") ?? [];
            if (selectedId.HasValue) PickupPointBox.SelectedValue = selectedId.Value;
            if (PickupPointBox.SelectedIndex < 0 && PickupPointBox.Items.Count > 0) PickupPointBox.SelectedIndex = 0;
        }
        catch (Exception ex) { StatusText.Text = "Не удалось загрузить пункты выдачи: " + ex.Message; }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new LoginWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login", new { UserName = dialog.UserName, Password = dialog.Password });
            if (!response.IsSuccessStatusCode) { StatusText.Text = "Не удалось войти: проверьте логин и пароль."; return; }
            var account = await response.Content.ReadFromJsonAsync<AccountView>();
            AccountText.Text = account is null ? "Вход выполнен" : $"{account.UserName} ({account.Role})";
            AdminButton.Visibility = account?.Role == "Admin" ? Visibility.Visible : Visibility.Collapsed;
            await LoadCartAsync();
            StatusText.Text = "Вход выполнен. Корзина и оформление заказа доступны.";
        }
        catch (Exception ex) { StatusText.Text = "Не удалось подключиться к API: " + ex.Message; }
    }

    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RegisterWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/register", new { dialog.UserName, dialog.Password, PasswordRepeat = dialog.PasswordRepeat });
            StatusText.Text = response.IsSuccessStatusCode ? "Регистрация выполнена. Войдите в аккаунт." : "Не удалось зарегистрироваться: проверьте логин и пароли.";
        }
        catch (Exception ex) { StatusText.Text = "Не удалось подключиться к API: " + ex.Message; }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductView product) { StatusText.Text = "Выберите товар в каталоге."; return; }
        var response = await _http.PostAsJsonAsync("/api/cart", new { ProductId = product.Id, Quantity = 1 });
        if (!response.IsSuccessStatusCode) { StatusText.Text = "Войдите в систему или проверьте остаток товара."; return; }
        await LoadCartAsync();
        StatusText.Text = "Товар добавлен в корзину.";
    }

    private async void Favorites_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new FavoritesWindow(_http) { Owner = this };
            window.ShowDialog();
            await LoadCartAsync();
        }
        catch (Exception ex) { StatusText.Text = "Войдите в аккаунт, чтобы открыть избранное: " + ex.Message; }
    }

    private async void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductView product) { StatusText.Text = "Сначала выберите товар."; return; }
        var response = await _http.PostAsync($"/api/favorites/{product.Id}", null);
        StatusText.Text = response.IsSuccessStatusCode ? "Товар добавлен в избранное." : response.StatusCode == HttpStatusCode.Conflict ? "Товар уже в избранном." : "Войдите в аккаунт, чтобы добавить товар в избранное.";
    }

    private async void SubmitReview_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductView product) { StatusText.Text = "Сначала выберите товар."; return; }
        if (ReviewRatingBox.SelectedItem is not ComboBoxItem selected || !int.TryParse(selected.Content?.ToString(), out var rating)) { StatusText.Text = "Выберите оценку от 1 до 5."; return; }
        var response = await _http.PostAsJsonAsync($"/api/products/{product.Id}/reviews", new { Rating = rating, Text = ReviewTextBox.Text });
        if (response.StatusCode == HttpStatusCode.Conflict)
            response = await _http.PutAsJsonAsync($"/api/products/{product.Id}/reviews", new { Rating = rating, Text = ReviewTextBox.Text });
        if (!response.IsSuccessStatusCode) { StatusText.Text = response.StatusCode == HttpStatusCode.Unauthorized ? "Войдите, чтобы оставить отзыв." : "Не удалось сохранить отзыв. Проверьте оценку и текст."; return; }
        ReviewTextBox.Clear(); await LoadProductsAsync(SearchBox.Text); StatusText.Text = "Отзыв сохранён; средняя оценка товара обновлена.";
    }

    private async void Reviews_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductView product) { StatusText.Text = "Сначала выберите товар."; return; }
        try
        {
            var result = await _http.GetFromJsonAsync<ReviewsView>($"/api/products/{product.Id}/reviews");
            if (result is null || result.Items.Length == 0) { MessageBox.Show(this, "Отзывов пока нет.", "Отзывы о товаре"); return; }
            var text = new StringBuilder($"Средняя оценка: {result.AverageRating:0.##} из 5 · отзывов: {result.Count}{Environment.NewLine}{Environment.NewLine}");
            foreach (var review in result.Items) text.AppendLine($"{review.UserName} · {review.Rating}/5 · {review.CreatedAtUtc.ToLocalTime():g}").AppendLine(review.Text).AppendLine();
            MessageBox.Show(this, text.ToString(), "Отзывы о товаре");
        }
        catch (Exception ex) { StatusText.Text = "Не удалось загрузить отзывы: " + ex.Message; }
    }

    private async Task LoadCartAsync()
    {
        try
        {
            var items = await _http.GetFromJsonAsync<List<CartItemView>>("/api/cart") ?? [];
            _cart.Clear();
            _cart.AddRange(items.Select(x => new ProductView { Id = x.ProductId, Name = x.Name, Price = x.Price, Quantity = x.Quantity }));
            CartList.ItemsSource = null;
            CartList.ItemsSource = _cart;
        }
        catch { StatusText.Text = "Войдите в учётную запись, чтобы открыть корзину."; }
    }

    private async Task ChangeCartQuantityAsync(int change)
    {
        if (CartList.SelectedItem is not ProductView item) { StatusText.Text = "Выберите позицию в корзине."; return; }
        var quantity = item.Quantity + change;
        var response = await _http.PutAsJsonAsync($"/api/cart/{item.Id}", new { ProductId = item.Id, Quantity = quantity });
        if (!response.IsSuccessStatusCode) { StatusText.Text = response.StatusCode == System.Net.HttpStatusCode.Conflict ? "Недостаточно товара на складе." : "Не удалось изменить корзину. Проверьте вход."; return; }
        await LoadCartAsync();
        StatusText.Text = quantity <= 0 ? "Позиция удалена из корзины." : "Количество изменено.";
    }

    private async void IncreaseCart_Click(object sender, RoutedEventArgs e) => await ChangeCartQuantityAsync(1);
    private async void DecreaseCart_Click(object sender, RoutedEventArgs e) => await ChangeCartQuantityAsync(-1);

    private async void RemoveCart_Click(object sender, RoutedEventArgs e)
    {
        if (CartList.SelectedItem is not ProductView item) { StatusText.Text = "Выберите позицию в корзине."; return; }
        var response = await _http.DeleteAsync($"/api/cart/{item.Id}");
        if (!response.IsSuccessStatusCode) { StatusText.Text = "Не удалось удалить позицию. Проверьте вход."; return; }
        await LoadCartAsync();
        StatusText.Text = "Позиция удалена из корзины.";
    }

    private async void BuyNow_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductView product) { StatusText.Text = "Выберите товар в каталоге."; return; }
        var delivery = MessageBox.Show(this, "Оформить доставку по адресу? Для этого сумма должна быть больше 600 ₽. Нажмите «Нет» для самовывоза.", "Покупка товара", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (delivery == MessageBoxResult.Cancel) return;
        string? address = null;
        if (delivery == MessageBoxResult.Yes)
        {
            var dialog = new AddressWindow { Owner = this };
            if (dialog.ShowDialog() != true) return;
            address = dialog.Address;
        }
        var payload = delivery == MessageBoxResult.Yes
            ? new BuyRequest("AddressDelivery", address, null)
            : new BuyRequest("PickupPoint", null, PickupPointBox.SelectedValue as int?);
        if (delivery == MessageBoxResult.No && payload.PickupPointId is null) { StatusText.Text = "Нет доступного пункта выдачи."; return; }
        var response = await _http.PostAsJsonAsync($"/api/orders/buy/{product.Id}", payload);
        if (!response.IsSuccessStatusCode) { StatusText.Text = "Покупка не выполнена. Проверьте вход, остаток и способ получения."; return; }
        await LoadProductsAsync(SearchBox.Text);
        await LoadCartAsync();
        StatusText.Text = "Заказ оформлен. Он появится в истории заказов.";
    }

    private async void Delivery_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddressWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        await CheckoutAsync(new { Fulfillment = "AddressDelivery", Address = dialog.Address, PickupPointId = (int?)null });
    }

    private async void Pickup_Click(object sender, RoutedEventArgs e)
    {
        if (PickupPointBox.SelectedValue is not int pointId) { StatusText.Text = "Выберите действующий пункт выдачи."; return; }
        await CheckoutAsync(new { Fulfillment = "PickupPoint", Address = (string?)null, PickupPointId = (int?)pointId });
    }

    private async void History_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sort = (HistorySortBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "newest";
            var orders = await _http.GetFromJsonAsync<List<OrderView>>($"/api/orders?sort={sort}") ?? [];
            if (orders.Count == 0) { MessageBox.Show(this, "Заказов пока нет.", "История заказов"); return; }
            var details = new StringBuilder();
            foreach (var order in orders)
            {
                var full = await _http.GetFromJsonAsync<OrderDetailsView>($"/api/orders/{order.Id}");
                details.AppendLine($"Заказ №{order.Id} · {order.CreatedAtUtc:g} · {order.Status}");
                if (full is not null)
                    foreach (var item in full.Items)
                        details.AppendLine($"  {item.ProductNameAtPurchase} × {item.Quantity} — {item.LineTotal:N2} ₽");
                details.AppendLine($"Итого: {order.Total:N2} ₽");
                details.AppendLine();
            }
            MessageBox.Show(this, details.ToString(), "История заказов");
        }
        catch { StatusText.Text = "Войдите в аккаунт, чтобы открыть историю заказов."; }
    }

    private async Task CheckoutAsync<T>(T details)
    {
        var response = await _http.PostAsJsonAsync("/api/orders", details);
        if (!response.IsSuccessStatusCode) { StatusText.Text = "Заказ не оформлен. Проверьте вход, корзину и способ доставки."; return; }
        await LoadCartAsync(); StatusText.Text = "Заказ оформлен. Его можно найти в истории заказов.";
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        var window = new AdminWindow(_http) { Owner = this };
        window.ShowDialog();
        _ = LoadProductsAsync(SearchBox.Text);
        _ = LoadCategoriesAsync();
        _ = LoadPickupPointsAsync();
    }

    private sealed record ProductView
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string Category { get; init; } = "";
        public decimal Price { get; init; }
        public decimal Rating { get; init; }
        public int Stock { get; init; }
        public int Quantity { get; set; }
        public string QuantityLabel => $"Количество: {Quantity}";
        public string PriceLabel => $"Цена: {Price:N2} ₽";
        public string LineTotalLabel => $"Сумма: {Price * Quantity:N2} ₽";
    }
    private sealed record AccountView(int Id, string UserName, string Role);
    private sealed record CategoryView(int Id, string Name);
    private sealed record PickupPointView(int Id, string Name, string Address) { public string DisplayName => $"{Name} — {Address}"; }
    private sealed record OrderView(int Id, DateTime CreatedAtUtc, decimal Total, string Status);
    private sealed record CartItemView(int ProductId, string Name, string ImagePath, decimal Price, int Quantity, decimal LineTotal);
    private sealed record BuyRequest(string Fulfillment, string? Address, int? PickupPointId);
    private sealed record OrderDetailsView(int Id, DateTime CreatedAtUtc, decimal Total, string Status, OrderItemView[] Items);
    private sealed record OrderItemView(int ProductId, string ProductNameAtPurchase, decimal UnitPriceAtPurchase, int Quantity, decimal LineTotal);
    private sealed record ReviewsView(decimal AverageRating, int Count, ReviewView[] Items);
    private sealed record ReviewView(int Id, int Rating, string Text, DateTime CreatedAtUtc, string UserName);
}
