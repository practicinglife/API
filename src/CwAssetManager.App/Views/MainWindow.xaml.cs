using CwAssetManager.App.ViewModels;
using System.Windows;

namespace CwAssetManager.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
