using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;

namespace ShopSystem.Desktop;

public partial class FavoritesWindow : Window
{
    private readonly HttpClient _http;
    public FavoritesWindow(HttpClient http)
    {
        InitializeComponent();
        _http = http;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try { FavoritesList.ItemsSource = await _http.GetFromJsonAsync<List<FavoriteView>>("/api/favorites") ?? []; StatusText.Text = $"В избранном: {FavoritesList.Items.Count}"; }
        catch { StatusText.Text = "Войдите в аккаунт, чтобы открыть избранное."; }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (FavoritesList.SelectedItem is not FavoriteView item) { StatusText.Text = "Выберите товар."; return; }
        using var response = await _http.DeleteAsync($"/api/favorites/{item.ProductId}");
        if (!response.IsSuccessStatusCode) { StatusText.Text = "Не удалось удалить товар."; return; }
        await LoadAsync(); StatusText.Text = "Товар удалён из избранного.";
    }

    private async void MoveToCart_Click(object sender, RoutedEventArgs e)
    {
        if (FavoritesList.SelectedItem is not FavoriteView item) { StatusText.Text = "Выберите товар."; return; }
        using var response = await _http.PostAsJsonAsync("/api/cart", new { ProductId = item.ProductId, Quantity = 1 });
        if (!response.IsSuccessStatusCode) { StatusText.Text = "Не удалось добавить товар: проверьте остаток и доступность."; return; }
        StatusText.Text = "Товар добавлен в корзину.";
    }

    private sealed record FavoriteView(int ProductId, string Name, string Description, decimal Price, string ImagePath, int Stock, string Category, decimal Rating)
    {
        public string DisplayName => $"{Name} · {Category} · {Price:N2} ₽ · оценка {Rating:0.##}/5 · остаток {Stock}";
    }
}
