using System.Windows;

namespace ShopSystem.Desktop;

public partial class RegisterWindow : Window
{
    public string UserName => UserBox.Text.Trim();
    public string Password => PasswordBox.Password;
    public string PasswordRepeat => PasswordRepeatBox.Password;
    public RegisterWindow() => InitializeComponent();
    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (UserName.Length < 3 || Password.Length < 8 || Password != PasswordRepeat) { MessageBox.Show(this, "Проверьте логин и оба поля пароля."); return; }
        DialogResult = true; Close();
    }
}
