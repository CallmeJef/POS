using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Services;
using PosApp.ViewModels;

namespace PosApp.Views;

public partial class PaymentMethodsDialog : UserControl
{
    readonly ObservableCollection<PaymentRow> _rows = new();
    byte[]? _image;

    public PaymentMethodsDialog()
    {
        InitializeComponent();
        MethodList.ItemsSource = _rows;
        Reload();
    }

    void Reload()
    {
        _rows.Clear();
        foreach (var m in Db.GetPaymentMethods())
            _rows.Add(new PaymentRow(m, OnDelete));
        EmptyText.IsVisible = _rows.Count == 0;
    }

    void OnDelete(PaymentRow row) =>
        Nav.Confirm("Delete payment method", $"Remove \"{row.Name}\"?",
            () =>
            {
                Db.DeletePaymentMethod(row.Method.Id);
                Reload();
            },
            "Delete", danger: true);

    void UpdatePreview()
    {
        PreviewImage.Source = ImageUtil.ToBitmap(_image);
        NoImageText.IsVisible = PreviewImage.Source == null;
    }

    async void ChooseImage_Click(object? sender, RoutedEventArgs e)
    {
        var bytes = await Nav.PickImageAsync(this);
        if (bytes == null) return;
        _image = bytes;
        UpdatePreview();
    }

    void RemoveImage_Click(object? sender, RoutedEventArgs e)
    {
        _image = null;
        UpdatePreview();
    }

    void Add_Click(object? sender, RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0)
        {
            ErrorText.Text = "Payment method name is required.";
            ErrorText.IsVisible = true;
            return;
        }

        Db.AddPaymentMethod(name, DetailsBox.Text ?? "", _image);
        NameBox.Text = "";
        DetailsBox.Text = "";
        _image = null;
        UpdatePreview();
        ErrorText.IsVisible = false;
        Reload();
    }

    void Return_Click(object? sender, RoutedEventArgs e) => Nav.CloseDialog();
}
