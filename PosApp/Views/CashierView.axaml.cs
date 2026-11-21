using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PosApp.Models;
using PosApp.Services;
using PosApp.ViewModels;

namespace PosApp.Views;

public partial class CashierView : UserControl
{
    readonly ObservableCollection<CategoryButton> _categories = new();
    readonly ObservableCollection<ProductCard> _cards = new();
    readonly ObservableCollection<CartLine> _cart = new();
    readonly ObservableCollection<QueueEntry> _queue = new();
    readonly ObservableCollection<DiscountChip> _discounts = new();
    readonly ObservableCollection<PaymentChoice> _payments = new();
    readonly DispatcherTimer _clock;

    CategoryButton? _current;
    DiscountChip _discount;
    PaymentChoice? _payment;

    public CashierView()
    {
        InitializeComponent();
        UserNameText.Text = Session.CurrentUser?.Username;

        CategoryList.ItemsSource = _categories;
        ItemGrid.ItemsSource = _cards;
        CartList.ItemsSource = _cart;
        QueueList.ItemsSource = _queue;
        DiscountList.ItemsSource = _discounts;
        PaymentList.ItemsSource = _payments;

        _discounts.Add(new DiscountChip("No discount", DiscountTypes.None, PickDiscount));
        _discounts.Add(new DiscountChip("PWD 20%", DiscountTypes.Pwd, PickDiscount));
        _discounts.Add(new DiscountChip("Senior 20%", DiscountTypes.Senior, PickDiscount));
        _discount = _discounts[0];
        _discount.IsSelected = true;

        QueueNumberBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) QuickServe();
        };
        AmountBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) RefreshTotals();
        };

        LoadCategories();
        LoadPayments();
        LoadQueue();
        RefreshTotals();

        UpdateClock();
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clock.Tick += (_, _) => UpdateClock();
        _clock.Start();
        DetachedFromVisualTree += (_, _) => _clock.Stop();
    }

    // ------------------------------------------------------------ categories

    void LoadCategories()
    {
        _categories.Clear();
        foreach (var c in Db.GetCategories())
            _categories.Add(new CategoryButton(c, SelectCategory, _ => { }));

        // The cashier screen opens on the first category the admin created.
        SelectCategory(_categories.FirstOrDefault());
    }

    void SelectCategory(CategoryButton? button)
    {
        foreach (var c in _categories) c.IsSelected = ReferenceEquals(c, button);
        _current = button;
        LoadItems();
    }

    void LoadItems()
    {
        _cards.Clear();
        if (_current == null)
        {
            CategoryTitle.Text = "No categories yet";
            CategorySubtitle.Text = "";
            EmptyHint.Text = "The admin has not created any categories or items yet.";
            EmptyHint.IsVisible = true;
            return;
        }

        var products = Db.GetProducts(_current.Category.Id);
        foreach (var p in products) _cards.Add(new ProductCard(p, AddToOrder));

        CategoryTitle.Text = _current.Category.Name;
        CategorySubtitle.Text = products.Count == 1 ? "1 item" : $"{products.Count} items";
        EmptyHint.Text = "No items in this category yet.";
        EmptyHint.IsVisible = products.Count == 0;
    }

    // ------------------------------------------------------------------ cart

    void AddToOrder(ProductCard card)
    {
        // Reload so the latest price and customizations are used.
        var product = Db.GetProduct(card.Product.Id);
        if (product == null)
        {
            Nav.Message("Item unavailable", "This item was removed. The list has been refreshed.");
            LoadItems();
            return;
        }

        var line = new CartLine(product, RefreshTotals, RemoveLine);
        var existing = _cart.FirstOrDefault(l => l.Key == line.Key);
        if (existing != null)
            existing.Increase();
        else
            _cart.Add(line);
        RefreshTotals();
    }

    void RemoveLine(CartLine line)
    {
        _cart.Remove(line);
        RefreshTotals();
    }

    (decimal Subtotal, decimal Discount, decimal Total) Totals()
    {
        var subtotal = _cart.Sum(l => l.LineTotal);
        var discount = string.IsNullOrEmpty(_discount.Type)
            ? 0m
            : Math.Round(subtotal * DiscountTypes.Rate, 2, MidpointRounding.AwayFromZero);
        return (subtotal, discount, subtotal - discount);
    }

    void RefreshTotals()
    {
        var (subtotal, discount, total) = Totals();
        SubtotalText.Text = Money.Format(subtotal);
        DiscountLabel.Text = string.IsNullOrEmpty(_discount.Type) ? "Discount" : $"Discount ({_discount.Label})";
        DiscountText.Text = discount == 0 ? Money.Format(0) : "−" + Money.Format(discount);
        TotalText.Text = Money.Format(total);
        CartEmptyText.IsVisible = _cart.Count == 0;
        RefreshChange(total);
    }

    /// Change
    void RefreshChange(decimal total)
    {
        var typed = AmountBox.Text;
        if (string.IsNullOrWhiteSpace(typed))
        {
            ChangeLabel.Text = "Change";
            ChangeText.Text = "—";
            ChangeText.Foreground = Brush.Parse("#f50b0b");
        }
        else if (!Money.TryParse(typed, out var received) || received < 0)
        {
            ChangeLabel.Text = "Change";
            ChangeText.Text = "Invalid amount";
            ChangeText.Foreground = Brush.Parse("#B3261E");
        }
        else if (received < total)
        {
            ChangeLabel.Text = "Short by";
            ChangeText.Text = Money.Format(total - received);
            ChangeText.Foreground = Brush.Parse("#B3261E");
        }
        else
        {
            ChangeLabel.Text = "Change";
            ChangeText.Text = Money.Format(received - total);
            ChangeText.Foreground = Brush.Parse("#027342");
        }
    }

    void PickDiscount(DiscountChip chip)
    {
        foreach (var d in _discounts) d.IsSelected = ReferenceEquals(d, chip);
        _discount = chip;
        RefreshTotals();
    }

    void UpdateClock() =>
        ClockText.Text = DateTime.Now.ToString("ddd, MMM dd yyyy  ·  h:mm tt", CultureInfo.InvariantCulture);

    // -------------------------------------------------------------- payments

    void LoadPayments()
    {
        _payments.Clear();
        _payment = null;
        foreach (var m in Db.GetPaymentMethods())
            _payments.Add(new PaymentChoice(m, OnPaymentTapped));
        NoPaymentText.IsVisible = _payments.Count == 0;
    }

    /// <summary>Tapping a method shows its picture and details (e.g. a QR code) to the customer.</summary>
    void OnPaymentTapped(PaymentChoice choice) =>
        Nav.ShowDialog(new PaymentInfoDialog(choice.Method, () =>
        {
            foreach (var p in _payments) p.IsSelected = ReferenceEquals(p, choice);
            _payment = choice;
        }));

    // ----------------------------------------------------------- place order

    void PlaceOrder_Click(object? sender, RoutedEventArgs e)
    {
        if (_cart.Count == 0)
        {
            Nav.Message("Order is empty", "Add at least one item first.");
            return;
        }
        if (_payments.Count > 0 && _payment == null)
        {
            Nav.Message("Payment method", "Choose a payment method first.");
            return;
        }

        var (_, discount, total) = Totals();

        var typedAmount = AmountBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(typedAmount))
        {
            Nav.Message("Amount received", "Enter the amount received from the customer.");
            return;
        }

        if (!Money.TryParse(typedAmount, out var received) || received < 0)
        {
            Nav.Message("Amount received", "Enter the customer's money as a number.");
            return;
        }
        if (received < total)
        {
            Nav.Message("Not enough money",
                $"The customer is short by {Money.Format(total - received)}.");
            return;
        }
        var change = received - total;

        var order = new NewOrder
        {
            CashierName = Session.CurrentUser?.Username ?? "",
            DiscountType = _discount.Type,
            Discount = discount,
            Total = total,
            AmountPaid = received,
            PaymentMethod = _payment?.Name,
            Lines = _cart.Select(l => new NewOrderLine
            {
                ItemName = l.Name,
                Options = l.OptionsText,
                Qty = l.Qty,
                LineTotal = l.LineTotal
            }).ToList()
        };

        int number;
        try
        {
            number = Db.SaveOrder(order);
        }
        catch (Exception ex)
        {
            Nav.Message("Could not save the order", ex.Message);
            return;
        }

        ResetOrder();
        LoadQueue();
        Nav.Message($"Order #{number:000} placed",
            $"Queue number: #{number:000}\nTotal: {Money.Format(total)}\nReceived: {Money.Format(received)}\n" +
            $"Change: {Money.Format(change)}\n\nCall this number when the order is ready.");
    }

    void ResetOrder()
    {
        _cart.Clear();
        PickDiscount(_discounts[0]);
        foreach (var p in _payments) p.IsSelected = false;
        _payment = null;
        AmountBox.Text = "";
        RefreshTotals();
    }

    // ----------------------------------------------------------------- queue

    void LoadQueue()
    {
        _queue.Clear();
        foreach (var o in Db.GetQueue())
            _queue.Add(new QueueEntry(o, ServeEntry, VoidEntry));
        QueueCountText.Text = _queue.Count == 0 ? "" : $"{_queue.Count} waiting";
        QueueEmptyText.IsVisible = _queue.Count == 0;
    }

    /// <summary>The customer received the order: remove it from the queue (it stays in the ledger).</summary>
    void ServeEntry(QueueEntry entry) =>
        Nav.Confirm("Order received",
            $"Mark order {entry.Number} as received by the customer? It will be removed from the queue.",
            () =>
            {
                Db.MarkServed(entry.Order.Id);
                LoadQueue();
            },
            "Served");

    void VoidEntry(QueueEntry entry) =>
        Nav.ShowDialog(new VoidOrderDialog(entry.Order, LoadQueue));

    void QuickServe_Click(object? sender, RoutedEventArgs e) => QuickServe();

    void QuickServe()
    {
        var text = (QueueNumberBox.Text ?? "").Trim().TrimStart('#');
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            Nav.Message("Queue number", "Type the queue number of the order, for example 12.");
            return;
        }

        var entry = _queue
            .Where(q => q.Order.QueueNumber == number)
            .OrderByDescending(q => q.Order.CreatedAt)
            .FirstOrDefault();
        if (entry == null)
        {
            Nav.Message("Not found", $"No waiting order has queue number #{number:000}.");
            return;
        }

        QueueNumberBox.Text = "";
        ServeEntry(entry);
    }

    // ---------------------------------------------------------------- logout

    void Logout_Click(object? sender, RoutedEventArgs e)
    {
        if (_cart.Count > 0)
        {
            Nav.Confirm("Log out", "The current order has not been placed and will be discarded. Log out anyway?",
                DoLogout, "Log out", danger: true);
            return;
        }
        DoLogout();
    }

    static void DoLogout()
    {
        Session.CurrentUser = null;
        Nav.GoTo(new LoginView());
    }
}
