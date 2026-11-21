using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Models;
using PosApp.Services;

namespace PosApp.Views;

public partial class ItemEditorDialog : UserControl
{
    readonly Product? _existing;
    readonly long _categoryId;
    readonly Action<long> _onSaved;
    byte[]? _image;

    public ItemEditorDialog() : this(null, 0, _ => { }) { }

    /// <param name="existing">Null to create a new item, or a fully loaded product to edit.</param>
    public ItemEditorDialog(Product? existing, long categoryId, Action<long> onSaved)
    {
        InitializeComponent();
        _existing = existing;
        _categoryId = categoryId;
        _onSaved = onSaved;

        SizeEditor.Setup(OptionKinds.Size, "Size",
            "Portion sizes such as S, M, L.", true);
        AdditionEditor.Setup(OptionKinds.Addition, "Additions",
            "Additions, toppings.", false);

        if (existing != null)
        {
            TitleText.Text = "Edit Item";
            NameBox.Text = existing.Name;
            PriceBox.Text = existing.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            _image = existing.Image;
            SizeEditor.Load(existing.Options);
            AdditionEditor.Load(existing.Options);
        }
        UpdatePreview();
    }

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

    void Cancel_Click(object? sender, RoutedEventArgs e) => Nav.CloseDialog();

    void Save_Click(object? sender, RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0) { ShowError("Item name is required."); return; }
        if (!Money.TryParse(PriceBox.Text, out var price) || price < 0)
        {
            ShowError("Enter a valid base price, for example 120 or 120.50.");
            return;
        }
        if (_categoryId == 0) { ShowError("No category selected."); return; }

        var product = new Product
        {
            Id = _existing?.Id ?? 0,
            CategoryId = _categoryId,
            Name = name,
            Price = price,
            Image = _image
        };
        product.Options.AddRange(SizeEditor.GetOptions());
        product.Options.AddRange(AdditionEditor.GetOptions());

        var id = Db.SaveProduct(product);
        Nav.CloseDialog();
        _onSaved(id);
    }

    void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
