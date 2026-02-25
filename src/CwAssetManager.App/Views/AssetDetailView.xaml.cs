using CwAssetManager.App.ViewModels;
using System.Windows.Controls;

namespace CwAssetManager.App.Views;

public partial class AssetDetailView : UserControl
{
    public AssetDetailView(AssetDetailViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
