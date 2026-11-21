using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace PosApp.Services;

/// <summary>
/// In-app navigation. Pages are swapped inside one host and dialogs are overlays, so the same code
/// works on desktop windows and on Android (which has no secondary windows).
/// </summary>
public static class Nav
{
    static ContentControl? _page;
    static Panel? _layer;

    public static void Init(ContentControl pageHost, Panel dialogLayer)
    {
        _page = pageHost;
        _layer = dialogLayer;
    }

    public static void GoTo(Control page)
    {
        CloseAllDialogs();
        if (_page != null) _page.Content = page;
    }

    public static void ShowDialog(Control content)
    {
        if (_layer == null) return;
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        content.Margin = new Thickness(16);

        var backdrop = new Grid { Background = new SolidColorBrush(Color.Parse("#99000000")) };
        backdrop.Children.Add(content);
        _layer.Children.Add(backdrop);
        _layer.IsVisible = true;
    }

    /// <summary>Closes the top-most dialog.</summary>
    public static void CloseDialog()
    {
        if (_layer == null || _layer.Children.Count == 0) return;
        _layer.Children.RemoveAt(_layer.Children.Count - 1);
        _layer.IsVisible = _layer.Children.Count > 0;
    }

    static void CloseAllDialogs()
    {
        if (_layer == null) return;
        _layer.Children.Clear();
        _layer.IsVisible = false;
    }

    // ----------------------------------------------------------- simple dialogs

    public static void Message(string title, string message)
    {
        var ok = MakeButton("OK", "primary");
        ok.Click += (_, _) => CloseDialog();
        ShowDialog(BuildBox(title, message, null, ok));
    }

    /// <summary>Asks for confirmation. The dialog closes itself before <paramref name="onYes"/> runs.</summary>
    public static void Confirm(string title, string message, Action onYes, string yesText = "Yes", bool danger = false)
    {
        var yes = MakeButton(yesText, danger ? "danger" : "primary");
        var no = MakeButton("Cancel", "outline");
        yes.Click += (_, _) =>
        {
            CloseDialog();
            onYes();
        };
        no.Click += (_, _) => CloseDialog();
        ShowDialog(BuildBox(title, message, no, yes));
    }

    static Button MakeButton(string text, string cssClass)
    {
        var b = new Button { Content = text };
        b.Classes.Add(cssClass);
        return b;
    }

    static Control BuildBox(string title, string message, Button? left, Button right)
    {
        var buttons = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 8, 0, 0)
        };
        if (left != null)
        {
            Grid.SetColumn(left, 0);
            buttons.Children.Add(left);
        }
        Grid.SetColumn(right, 2);
        buttons.Children.Add(right);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#027342")
        });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(buttons);

        var card = new Border { Padding = new Thickness(24), Width = 380, Child = panel };
        card.Classes.Add("card");
        return card;
    }

    // ------------------------------------------------------------ image picking

    /// <summary>Opens the platform file picker and returns the chosen image, shrunk and encoded as PNG.</summary>
    public static async Task<byte[]?> PickImageAsync(Control owner)
    {
        var top = TopLevel.GetTopLevel(owner);
        if (top == null) return null;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an image",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });
        if (files.Count == 0) return null;

        await using var stream = await files[0].OpenReadAsync();
        var bytes = ImageUtil.Downscale(stream);
        if (bytes == null) Message("Image problem", "That file could not be read as an image.");
        return bytes;
    }
}
