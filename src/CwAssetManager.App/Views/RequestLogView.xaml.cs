using CwAssetManager.App.ViewModels;
using System.Windows.Controls;

namespace CwAssetManager.App.Views;

public partial class RequestLogView : UserControl
{
    public RequestLogView(RequestLogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
