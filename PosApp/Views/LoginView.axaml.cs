using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PosApp.Models;
using PosApp.Services;

namespace PosApp.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        UsernameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) PasswordBox.Focus();
        };
        PasswordBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) DoLogin();
        };
    }

    void Login_Click(object? sender, RoutedEventArgs e) => DoLogin();

    void DoLogin()
    {
        var user = Db.Authenticate(UsernameBox.Text ?? "", PasswordBox.Text ?? "");
        if (user == null)
        {
            ErrorText.Text = "Incorrect username or password.";
            ErrorText.IsVisible = true;
            PasswordBox.Text = "";
            return;
        }

        Session.CurrentUser = user;
        if (user.Role == Roles.Admin)
            Nav.GoTo(new AdminView());
        else
            Nav.GoTo(new CashierView());
    }
}
