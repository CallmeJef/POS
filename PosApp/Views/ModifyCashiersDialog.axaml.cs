using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Models;
using PosApp.Services;

namespace PosApp.Views;

public partial class ModifyCashiersDialog : UserControl
{
    readonly ObservableCollection<UserAccount> _cashiers = new();
    UserAccount? _selected;

    public ModifyCashiersDialog()
    {
        InitializeComponent();
        CashierList.ItemsSource = _cashiers;
        Reload(null);
    }

    void Reload(long? keepSelectedId)
    {
        _cashiers.Clear();
        foreach (var u in Db.GetCashiers()) _cashiers.Add(u);

        HintText.Text = _cashiers.Count == 0 ? "No cashier accounts yet." : "Select a cashier to edit.";
        CashierList.SelectedItem = keepSelectedId == null
            ? null
            : _cashiers.FirstOrDefault(u => u.Id == keepSelectedId);
    }

    void CashierList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = CashierList.SelectedItem as UserAccount;
        EditPanel.IsVisible = _selected != null;
        HintText.IsVisible = _selected == null;
        ErrorText.IsVisible = false;
        StatusText.IsVisible = false;
        if (_selected != null)
        {
            UserBox.Text = _selected.Username;
            PassBox.Text = "";
        }
    }

    void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var id = _selected.Id;
        var newPassword = string.IsNullOrEmpty(PassBox.Text) ? null : PassBox.Text;

        var error = Db.UpdateCashier(id, UserBox.Text ?? "", newPassword);
        if (error != null)
        {
            StatusText.IsVisible = false;
            ErrorText.Text = error;
            ErrorText.IsVisible = true;
            return;
        }

        Reload(id);
        StatusText.Text = "Changes saved.";
        StatusText.IsVisible = true;
    }

    void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var target = _selected;
        Nav.Confirm("Delete cashier", $"Delete the account \"{target.Username}\"?",
            () =>
            {
                Db.DeleteCashier(target.Id);
                Reload(null);
            },
            "Delete", danger: true);
    }

    void Return_Click(object? sender, RoutedEventArgs e) => Nav.CloseDialog();
}
