using System.Globalization;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using PosApp.Models;
using PosApp.Services;

namespace PosApp.ViewModels;

/// <summary>An item card in the cashier's center grid.</summary>
public sealed class ProductCard
{
    public ProductCard(Product product, Action<ProductCard> onAdd)
    {
        Product = product;
        Image = ImageUtil.ToBitmap(product.Image);
        AddCommand = new RelayCommand(() => onAdd(this));
    }

    public Product Product { get; }
    public string Name => Product.Name;
    public string Initial => Product.Name.Length > 0 ? Product.Name.Substring(0, 1).ToUpperInvariant() : "?";
    public string PriceText => Money.Format(Product.Price);
    public Bitmap? Image { get; }
    public bool HasImage => Image != null;
    public ICommand AddCommand { get; }
}

/// <summary>A size or addition the cashier can pick for an order line.</summary>
public sealed class OptionChoice : Observable
{
    bool _isSelected;

    public OptionChoice(CustomOption option, Action<OptionChoice>? onPick = null)
    {
        Option = option;
        SelectCommand = new RelayCommand(() => onPick?.Invoke(this));
    }

    public CustomOption Option { get; }
    public string Label => Option.PriceDelta == 0 ? Option.Name : $"{Option.Name} (+{Money.Format(Option.PriceDelta)})";
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public ICommand SelectCommand { get; }
}

/// <summary>One line in the checkout panel: an item, its quantity and chosen customizations.</summary>
public sealed class CartLine : Observable
{
    readonly Action _changed;
    readonly Action<CartLine> _remove;
    int _qty = 1;
    bool _expanded;

    public CartLine(Product product, Action changed, Action<CartLine> remove)
    {
        Product = product;
        _changed = changed;
        _remove = remove;

        Sizes = product.Options.Where(o => o.Kind == OptionKinds.Size)
            .Select(o => new OptionChoice(o, PickSize)).ToList();
        Additions = product.Options.Where(o => o.Kind == OptionKinds.Addition)
            .Select(o => new OptionChoice(o)).ToList();

        // The first size (usually S) is preselected so every line has a valid price.
        if (Sizes.Count > 0) Sizes[0].IsSelected = true;
        foreach (var a in Additions)
            a.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(OptionChoice.IsSelected)) Refresh();
            };

        IncreaseCommand = new RelayCommand(Increase);
        DecreaseCommand = new RelayCommand(Decrease);
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public Product Product { get; }
    public string Name => Product.Name;
    public List<OptionChoice> Sizes { get; }
    public List<OptionChoice> Additions { get; }

    public int Qty => _qty;

    IEnumerable<OptionChoice> Selected => Sizes.Concat(Additions).Where(o => o.IsSelected);

    public decimal UnitPrice => Product.Price + Selected.Sum(o => o.Option.PriceDelta);
    public decimal LineTotal => UnitPrice * _qty;
    public string UnitPriceText => Money.Format(UnitPrice) + " each";
    public string LineTotalText => Money.Format(LineTotal);

    /// <summary>"M, Extra chocolate" – stored with the order and shown under the name.</summary>
    public string OptionsText => string.Join(", ", Selected.Select(o => o.Option.Name));
    public bool HasOptionsText => Selected.Any();

    /// <summary>Two lines with the same key are the same order line, so adding again just raises the quantity.</summary>
    public string Key =>
        Product.Id.ToString(CultureInfo.InvariantCulture) + "|" +
        string.Join(",", Selected.Select(o => o.Option.Id).OrderBy(i => i));

    public bool HasSizes => Sizes.Count > 0;
    public bool HasAdditions => Additions.Count > 0;
    public bool NoCustomizations => Sizes.Count == 0 && Additions.Count == 0;

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (Set(ref _expanded, value)) Raise(nameof(ExpandIcon));
        }
    }

    public string ExpandIcon => _expanded ? "–" : "+";

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
    public ICommand ToggleCommand { get; }

    void PickSize(OptionChoice picked)
    {
        foreach (var s in Sizes) s.IsSelected = ReferenceEquals(s, picked);
        Refresh();
    }

    public void Increase()
    {
        _qty++;
        Refresh();
    }

    /// <summary>Lowering the quantity below 1 removes the item from the order.</summary>
    void Decrease()
    {
        if (_qty <= 1)
        {
            _remove(this);
            return;
        }
        _qty--;
        Refresh();
    }

    void Refresh()
    {
        Raise(nameof(Qty));
        Raise(nameof(UnitPrice));
        Raise(nameof(UnitPriceText));
        Raise(nameof(LineTotal));
        Raise(nameof(LineTotalText));
        Raise(nameof(OptionsText));
        Raise(nameof(HasOptionsText));
        _changed();
    }
}

/// <summary>Discount selector chip: none, PWD 20% or Senior 20%.</summary>
public sealed class DiscountChip : Observable
{
    bool _isSelected;

    public DiscountChip(string label, string type, Action<DiscountChip> onPick)
    {
        Label = label;
        Type = type;
        SelectCommand = new RelayCommand(() => onPick(this));
    }

    public string Label { get; }
    public string Type { get; }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public ICommand SelectCommand { get; }
}

/// <summary>Payment method chip shown in the checkout panel.</summary>
public sealed class PaymentChoice : Observable
{
    bool _isSelected;

    public PaymentChoice(PaymentMethod method, Action<PaymentChoice> onTap)
    {
        Method = method;
        SelectCommand = new RelayCommand(() => onTap(this));
    }

    public PaymentMethod Method { get; }
    public string Name => Method.Name;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public ICommand SelectCommand { get; }
}

/// <summary>An order waiting in the queue panel.</summary>
public sealed class QueueEntry
{
    public QueueEntry(QueueOrder order, Action<QueueEntry> onServe, Action<QueueEntry> onVoid)
    {
        Order = order;
        ServeCommand = new RelayCommand(() => onServe(this));
        VoidCommand = new RelayCommand(() => onVoid(this));
    }

    public QueueOrder Order { get; }
    public string Number => "#" + Order.QueueNumber.ToString("000", CultureInfo.InvariantCulture);

    /// <summary>Time for today's orders; date and time for anything left over from an earlier day.</summary>
    public string TimeText => Order.CreatedAt.Date == DateTime.Today
        ? Order.CreatedAt.ToString("h:mm tt", CultureInfo.InvariantCulture)
        : Order.CreatedAt.ToString("MMM dd, h:mm tt", CultureInfo.InvariantCulture);

    public string Summary => Order.Summary;
    public string TotalText => Money.Format(Order.Total);
    public ICommand ServeCommand { get; }
    public ICommand VoidCommand { get; }
}
