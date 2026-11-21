using Avalonia.Controls;
using PosApp.Services;

namespace PosApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Nav.Init(PageHost, DialogLayer);
        Nav.GoTo(new LoginView());
    }
}
