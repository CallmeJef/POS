using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Models;
using PosApp.Services;
using PosApp.ViewModels;

namespace PosApp.Views;

public partial class LedgerView : UserControl
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    readonly Control _returnTo;
    readonly List<OrderRecord> _all;
    readonly ObservableCollection<LedgerRow> _rows = new();

    public LedgerView() : this(new Control()) { }

    /// <param name="returnTo">The page to go back to when "Return" is pressed.</param>
    public LedgerView(Control returnTo)
    {
        InitializeComponent();
        _returnTo = returnTo;
        _all = Db.GetOrders();
        LedgerList.ItemsSource = _rows;

        var months = _all
            .Select(o => new DateTime(o.CreatedAt.Year, o.CreatedAt.Month, 1))
            .Distinct()
            .OrderByDescending(m => m)
            .Select(m => m.ToString("MMMM yyyy", Inv));
        MonthBox.ItemsSource = new[] { "All months" }.Concat(months).ToList();
        MonthBox.SelectedIndex = 0;
        Rebuild();
    }

    void MonthBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => Rebuild();

    void Rebuild()
    {
        if (_rows == null) return;

        IEnumerable<OrderRecord> orders = _all;
        if (MonthBox.SelectedIndex > 0 && MonthBox.SelectedItem is string month)
            orders = orders.Where(o => o.CreatedAt.ToString("MMMM yyyy", Inv) == month);
        var list = orders.OrderByDescending(o => o.CreatedAt).ToList();

        _rows.Clear();
        var groups = list
            .GroupBy(o => new DateTime(o.CreatedAt.Year, o.CreatedAt.Month, 1))
            .OrderByDescending(g => g.Key);
        foreach (var g in groups)
        {
            var monthTotal = g.Sum(o => o.Total);
            _rows.Add(new LedgerRow
            {
                IsHeader = true,
                HeaderText = $"{g.Key.ToString("MMMM yyyy", Inv)}  ·  {g.Count()} order(s)  ·  {Money.Format(monthTotal)}"
            });
            foreach (var o in g)
            {
                _rows.Add(new LedgerRow
                {
                    Date = o.CreatedAt.ToString("MMM dd", Inv),
                    Time = o.CreatedAt.ToString("h:mm tt", Inv),
                    Year = o.CreatedAt.ToString("yyyy", Inv),
                    Cashier = o.CashierName,
                    Item = o.Summary,
                    TotalText = Money.Format(o.Total)
                });
            }
        }

        EmptyText.IsVisible = list.Count == 0;
        GoodsText.Text = $"Goods sold: {list.Sum(o => o.Quantity)}";
        TotalText.Text = $"Total purchases: {Money.Format(list.Sum(o => o.Total))}";
    }

    void Return_Click(object? sender, RoutedEventArgs e) => Nav.GoTo(_returnTo);
}
