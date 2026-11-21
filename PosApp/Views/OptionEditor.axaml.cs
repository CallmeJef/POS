using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PosApp.Models;
using PosApp.Services;
using PosApp.ViewModels;

namespace PosApp.Views;

public partial class OptionEditor : UserControl
{
    string _kind = OptionKinds.Size;

    public ObservableCollection<OptionRow> Rows { get; } = new();

    public OptionEditor()
    {
        InitializeComponent();
        OptionList.ItemsSource = Rows;
        Rows.CollectionChanged += (_, _) => EmptyText.IsVisible = Rows.Count == 0;
    }

    public void Setup(string kind, string title, string hint, bool showSizePreset)
    {
        _kind = kind;
        TitleText.Text = title;
        HintText.Text = hint;
        PresetButton.IsVisible = showSizePreset;
    }

    public void Load(IEnumerable<CustomOption> options)
    {
        Rows.Clear();
        foreach (var o in options.Where(o => o.Kind == _kind))
            Rows.Add(new OptionRow(o));
    }

    public List<CustomOption> GetOptions() => Rows.Select(r => r.Option).ToList();

    bool Exists(string name) =>
        Rows.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    void Add_Click(object? sender, RoutedEventArgs e)
    {
        var name = (NewName.Text ?? "").Trim();
        if (name.Length == 0)
        {
            Nav.Message("Name needed", "Type a name first.");
            return;
        }
        if (Exists(name))
        {
            Nav.Message("Already added", $"\"{name}\" is already in this list.");
            return;
        }

        decimal delta = 0;
        if (!string.IsNullOrWhiteSpace(NewPrice.Text) && (!Money.TryParse(NewPrice.Text, out delta) || delta < 0))
        {
            Nav.Message("Check the price", "Enter the extra charge as a number (leave blank for no extra charge).");
            return;
        }

        Rows.Add(new OptionRow(new CustomOption { Kind = _kind, Name = name, PriceDelta = delta }));
        NewName.Text = "";
        NewPrice.Text = "";
    }

    void Preset_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var name in new[] { "S", "M", "L" })
            if (!Exists(name))
                Rows.Add(new OptionRow(new CustomOption { Kind = _kind, Name = name, PriceDelta = 0 }));
    }

    void DeleteChecked_Click(object? sender, RoutedEventArgs e)
    {
        var ticked = Rows.Where(r => r.IsChecked).ToList();
        if (ticked.Count == 0)
        {
            Nav.Message("Nothing ticked", "Tick the entries you want to remove, then press Delete.");
            return;
        }
        foreach (var r in ticked) Rows.Remove(r);
    }
}
