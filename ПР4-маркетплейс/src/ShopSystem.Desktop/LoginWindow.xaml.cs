using System.Windows;

namespace ShopSystem.Desktop;

public partial class LoginWindow : Window
{
    public string UserName => UserBox.Text.Trim();
    public string Password => PasswordBox.Password;
    public LoginWindow() => InitializeComponent();
    private void Submit_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
}
