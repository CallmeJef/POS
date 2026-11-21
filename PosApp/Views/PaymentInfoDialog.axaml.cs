using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Models;
using PosApp.Services;

namespace PosApp.Views;

/// <summary>Shows a payment method's picture (e.g. a QR code) and details to the customer.</summary>
public partial class PaymentInfoDialog : UserControl
{
    readonly Action _onUse;

    public PaymentInfoDialog() : this(new PaymentMethod { Name = "Payment" }, () => { }) { }

    public PaymentInfoDialog(PaymentMethod method, Action onUse)
    {
        InitializeComponent();
        _onUse = onUse;

        NameText.Text = method.Name;
        UseButton.Content = $"Use {method.Name}";

        var image = ImageUtil.ToBitmap(method.Image);
        MethodImage.Source = image;
        ImageBox.IsVisible = image != null;

        DetailsText.Text = method.Details;
        DetailsText.IsVisible = !string.IsNullOrWhiteSpace(method.Details);
    }

    void Cancel_Click(object? sender, RoutedEventArgs e) => Nav.CloseDialog();

    void Use_Click(object? sender, RoutedEventArgs e)
    {
        Nav.CloseDialog();
        _onUse();
    }
}
