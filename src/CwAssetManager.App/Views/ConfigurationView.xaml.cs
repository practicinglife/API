using CwAssetManager.App.ViewModels;
using System.Windows.Controls;

namespace CwAssetManager.App.Views;

public partial class ConfigurationView : UserControl
{
    public ConfigurationView(ConfigurationViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
