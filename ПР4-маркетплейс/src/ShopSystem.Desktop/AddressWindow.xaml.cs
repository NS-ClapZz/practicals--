using System.Windows;

namespace ShopSystem.Desktop;

public partial class AddressWindow : Window
{
    public string Address => AddressBox.Text.Trim();
    public AddressWindow() => InitializeComponent();
    private void Submit_Click(object sender, RoutedEventArgs e) { if (Address.Length == 0) { MessageBox.Show(this, "Введите адрес."); return; } DialogResult = true; Close(); }
}
