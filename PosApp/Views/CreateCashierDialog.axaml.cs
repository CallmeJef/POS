using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Services;

namespace PosApp.Views;

public partial class CreateCashierDialog : UserControl
{
    public CreateCashierDialog() => InitializeComponent();

    void Cancel_Click(object? sender, RoutedEventArgs e) => Nav.CloseDialog();

    void Create_Click(object? sender, RoutedEventArgs e)
    {
        var password = PassBox.Text ?? "";
        if (password != (ConfirmBox.Text ?? ""))
        {
            ErrorText.Text = "The two passwords do not match.";
            ErrorText.IsVisible = true;
            return;
        }

        var username = (UserBox.Text ?? "").Trim();
        var error = Db.CreateCashier(username, password);
        if (error != null)
        {
            ErrorText.Text = error;
            ErrorText.IsVisible = true;
            return;
        }

        Nav.CloseDialog();
        Nav.Message("Cashier created", $"Account \"{username}\" can now log in.");
    }
}
