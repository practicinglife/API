using CwAssetManager.App.ViewModels;
using System.Windows.Controls;

namespace CwAssetManager.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
