using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PosApp.Models;
using PosApp.Services;

namespace PosApp.Views;

public partial class VoidOrderDialog : UserControl
{
    readonly QueueOrder _order;
    readonly Action _onVoided;

    public VoidOrderDialog() : this(new QueueOrder(), () => { }) { }

    public VoidOrderDialog(QueueOrder order, Action onVoided)
    {
        InitializeComponent();
        _order = order;
        _onVoided = onVoided;

        TitleText.Text = $"Void order #{order.QueueNumber:000}";
        SummaryText.Text = $"{order.Summary}\nTotal: {Money.Format(order.Total)}";
        CodeBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) DoVoid();
        };
    }

    void Cancel_Click(object? sender, RoutedEventArgs e) => Nav.CloseDialog();

    void Void_Click(object? sender, RoutedEventArgs e) => DoVoid();

    void DoVoid()
    {
        var error = Db.VoidOrder(_order.Id, CodeBox.Text ?? "", Session.CurrentUser?.Username ?? "");
        if (error != null)
        {
            ErrorText.Text = error;
            ErrorText.IsVisible = true;
            CodeBox.Text = "";
            return;
        }

        Nav.CloseDialog();
        _onVoided();
    }
}
