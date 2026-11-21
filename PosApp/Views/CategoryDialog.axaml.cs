using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Models;
using PosApp.Services;

namespace PosApp.Views;

public partial class CategoryDialog : UserControl
{
    readonly Category? _existing;
    readonly Action<long> _onSaved;
    readonly Action? _onDeleted;

    public CategoryDialog() : this(null, _ => { }, null) { }

    /// <param name="existing">Null to create a new category, or the category to rename/delete.</param>
    public CategoryDialog(Category? existing, Action<long> onSaved, Action? onDeleted)
    {
        InitializeComponent();
        _existing = existing;
        _onSaved = onSaved;
        _onDeleted = onDeleted;

        if (existing != null)
        {
            TitleText.Text = "Edit Category";
            NameBox.Text = existing.Name;
            DeleteButton.IsVisible = true;
        }
    }

    void Cancel_Click(object? sender, RoutedEventArgs e) => Nav.CloseDialog();

    void Save_Click(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text ?? "";
        string? error;
        long id;
        if (_existing == null)
        {
            error = Db.AddCategory(name, out id);
        }
        else
        {
            id = _existing.Id;
            error = Db.RenameCategory(id, name);
        }

        if (error != null)
        {
            ErrorText.Text = error;
            ErrorText.IsVisible = true;
            return;
        }

        Nav.CloseDialog();
        _onSaved(id);
    }

    void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_existing == null) return;
        Nav.Confirm("Delete category",
            $"Delete \"{_existing.Name}\" and ALL items inside it? This cannot be undone.",
            () =>
            {
                Db.DeleteCategory(_existing.Id);
                Nav.CloseDialog(); // this dialog
                _onDeleted?.Invoke();
            },
            "Delete", danger: true);
    }
}
