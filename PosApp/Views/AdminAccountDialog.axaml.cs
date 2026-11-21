using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Services;

namespace PosApp.Views;

public partial class AdminAccountDialog : UserControl
{
    readonly Action _onSaved;

    public AdminAccountDialog() : this(() => { }) { }

    public AdminAccountDialog(Action onSaved)
    {
        InitializeComponent();
        _onSaved = onSaved;
        UserBox.Text = Session.CurrentUser?.Username;
    }

    void Cancel_Click(object? sender, RoutedEventArgs e) => Nav.CloseDialog();

    void Save_Click(object? sender, RoutedEventArgs e)
    {
        var user = Session.CurrentUser;
        if (user == null) return;

        var newPassword = string.IsNullOrEmpty(NewPassBox.Text) ? null : NewPassBox.Text;
        if (newPassword != null && newPassword != (ConfirmBox.Text ?? ""))
        {
            ShowError("The new passwords do not match.");
            return;
        }

        var newName = (UserBox.Text ?? "").Trim();
        var newVoid = string.IsNullOrEmpty(VoidBox.Text) ? null : VoidBox.Text;
        var error = Db.UpdateAdmin(user.Id, newName, CurrentBox.Text ?? "", newPassword, newVoid);
        if (error != null)
        {
            ShowError(error);
            return;
        }

        user.Username = newName;
        Nav.CloseDialog();
        _onSaved();
        Nav.Message("Saved", "Admin account updated.");
    }

    void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
