using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Services;
using PosApp.ViewModels;

namespace PosApp.Views;

public partial class AdminView : UserControl
{
    readonly ObservableCollection<CategoryButton> _categories = new();
    readonly ObservableCollection<object> _gridItems = new();
    CategoryButton? _current;
    bool _selectMode;

    public AdminView()
    {
        InitializeComponent();
        UserNameText.Text = Session.CurrentUser?.Username;
        CategoryList.ItemsSource = _categories;
        ItemGrid.ItemsSource = _gridItems;
        LoadCategories(null);
    }

    IEnumerable<ItemCard> Cards => _gridItems.OfType<ItemCard>();

    // ------------------------------------------------------------ categories

    void LoadCategories(long? selectId)
    {
        _categories.Clear();
        foreach (var c in Db.GetCategories())
            _categories.Add(new CategoryButton(c, SelectCategory, EditCategory));

        var target = _categories.FirstOrDefault(x => x.Category.Id == selectId) ?? _categories.FirstOrDefault();
        SelectCategory(target);
    }

    void SelectCategory(CategoryButton? button)
    {
        foreach (var c in _categories) c.IsSelected = ReferenceEquals(c, button);
        _current = button;
        ExitSelectMode();
        LoadItems();
    }

    void CreateCategory_Click(object? sender, RoutedEventArgs e) =>
        Nav.ShowDialog(new CategoryDialog(null, id => LoadCategories(id), null));

    void EditCategory(CategoryButton button) =>
        Nav.ShowDialog(new CategoryDialog(button.Category, id => LoadCategories(id), () => LoadCategories(null)));

    // ----------------------------------------------------------------- items

    void LoadItems()
    {
        _gridItems.Clear();
        if (_current == null)
        {
            CategoryTitle.Text = "No categories yet";
            CategorySubtitle.Text = "";
            EmptyHint.IsVisible = true;
            DeleteButton.IsEnabled = false;
            return;
        }

        EmptyHint.IsVisible = false;
        DeleteButton.IsEnabled = true;
        _gridItems.Add(new CreateTile(new RelayCommand(CreateItem)));

        var products = Db.GetProducts(_current.Category.Id);
        foreach (var p in products) _gridItems.Add(new ItemCard(p, OnCardTap));

        CategoryTitle.Text = _current.Category.Name;
        CategorySubtitle.Text = products.Count == 1 ? "1 item" : $"{products.Count} items";
    }

    void CreateItem()
    {
        if (_current == null) return;
        ExitSelectMode();
        Nav.ShowDialog(new ItemEditorDialog(null, _current.Category.Id, _ => LoadItems()));
    }

    void OnCardTap(ItemCard card)
    {
        if (_selectMode)
        {
            card.IsSelected = !card.IsSelected;
            UpdateDeleteLabel();
            return;
        }

        var product = Db.GetProduct(card.Product.Id);
        if (product != null)
            Nav.ShowDialog(new ItemEditorDialog(product, product.CategoryId, _ => LoadItems()));
    }

    // ----------------------------------------------------------- bulk delete

    void EnterSelectMode()
    {
        _selectMode = true;
        foreach (var c in Cards)
        {
            c.IsSelected = false;
            c.SelectMode = true;
        }
        CancelDeleteButton.IsVisible = true;
        UpdateDeleteLabel();
    }

    void ExitSelectMode()
    {
        _selectMode = false;
        foreach (var c in Cards)
        {
            c.SelectMode = false;
            c.IsSelected = false;
        }
        CancelDeleteButton.IsVisible = false;
        UpdateDeleteLabel();
    }

    void UpdateDeleteLabel() =>
        DeleteButton.Content = _selectMode
            ? $"Delete Selected ({Cards.Count(c => c.IsSelected)})"
            : "Delete Items";

    void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (!_selectMode)
        {
            EnterSelectMode();
            return;
        }

        var picked = Cards.Where(c => c.IsSelected).ToList();
        if (picked.Count == 0)
        {
            Nav.Message("Nothing selected", "Tap the items you want to delete first.");
            return;
        }

        Nav.Confirm("Delete items",
            $"Delete {picked.Count} selected item(s)? This cannot be undone.",
            () =>
            {
                Db.DeleteProducts(picked.Select(c => c.Product.Id).ToList());
                ExitSelectMode();
                LoadItems();
            },
            "Delete", danger: true);
    }

    void CancelDelete_Click(object? sender, RoutedEventArgs e) => ExitSelectMode();

    // ----------------------------------------------------------- right panel

    void Ledger_Click(object? sender, RoutedEventArgs e) => Nav.GoTo(new LedgerView(this));

    void PaymentMethods_Click(object? sender, RoutedEventArgs e) => Nav.ShowDialog(new PaymentMethodsDialog());

    void CreateCashier_Click(object? sender, RoutedEventArgs e) => Nav.ShowDialog(new CreateCashierDialog());

    void ModifyCashiers_Click(object? sender, RoutedEventArgs e) => Nav.ShowDialog(new ModifyCashiersDialog());

    void AdminSettings_Click(object? sender, RoutedEventArgs e) =>
        Nav.ShowDialog(new AdminAccountDialog(() => UserNameText.Text = Session.CurrentUser?.Username));

    void Logout_Click(object? sender, RoutedEventArgs e)
    {
        Session.CurrentUser = null;
        Nav.GoTo(new LoginView());
    }
}
