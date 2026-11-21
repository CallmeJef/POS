using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using PosApp.Models;
using PosApp.Services;

namespace PosApp.ViewModels;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}

/// <summary>A category entry in the left sidebar.</summary>
public sealed class CategoryButton : Observable
{
    bool _isSelected;

    public CategoryButton(Category category, Action<CategoryButton> onSelect, Action<CategoryButton> onEdit)
    {
        Category = category;
        SelectCommand = new RelayCommand(() => onSelect(this));
        EditCommand = new RelayCommand(() => onEdit(this));
    }

    public Category Category { get; }
    public string Name => Category.Name;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public ICommand SelectCommand { get; }
    public ICommand EditCommand { get; }
}

/// <summary>The "Create Item" tile that always sits first in the grid.</summary>
public sealed class CreateTile
{
    public CreateTile(ICommand command) => Command = command;
    public ICommand Command { get; }
}

/// <summary>An item card in the center grid.</summary>
public sealed class ItemCard : Observable
{
    bool _selectMode;
    bool _isSelected;

    public ItemCard(Product product, Action<ItemCard> onTap)
    {
        Product = product;
        Image = ImageUtil.ToBitmap(product.Image);
        TapCommand = new RelayCommand(() => onTap(this));
    }

    public Product Product { get; }
    public string Name => Product.Name;
    public string Initial => Product.Name.Length > 0 ? Product.Name.Substring(0, 1).ToUpperInvariant() : "?";
    public string PriceText => Money.Format(Product.Price);
    public Bitmap? Image { get; }
    public bool HasImage => Image != null;
    public ICommand TapCommand { get; }

    public bool SelectMode { get => _selectMode; set => Set(ref _selectMode, value); }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
}

/// <summary>One Size/Addition option row in the item editor.</summary>
public sealed class OptionRow : Observable
{
    bool _isChecked;

    public OptionRow(CustomOption option) => Option = option;

    public CustomOption Option { get; }
    public string Name => Option.Name;
    public string PriceText => Option.PriceDelta == 0 ? "No extra charge" : "+" + Money.Format(Option.PriceDelta);
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }
}

/// <summary>A saved payment method row.</summary>
public sealed class PaymentRow
{
    public PaymentRow(PaymentMethod method, Action<PaymentRow> onDelete)
    {
        Method = method;
        Image = ImageUtil.ToBitmap(method.Image);
        DeleteCommand = new RelayCommand(() => onDelete(this));
    }

    public PaymentMethod Method { get; }
    public string Name => Method.Name;
    public string Details => Method.Details;
    public bool HasDetails => !string.IsNullOrWhiteSpace(Method.Details);
    public Bitmap? Image { get; }
    public bool HasImage => Image != null;
    public ICommand DeleteCommand { get; }
}

/// <summary>A row in the ledger table: either a month header or an order.</summary>
public sealed class LedgerRow
{
    public bool IsHeader { get; init; }
    public bool IsData => !IsHeader;
    public string HeaderText { get; init; } = "";
    public string Date { get; init; } = "";
    public string Time { get; init; } = "";
    public string Year { get; init; } = "";
    public string Cashier { get; init; } = "";
    public string Item { get; init; } = "";
    public string TotalText { get; init; } = "";
}
