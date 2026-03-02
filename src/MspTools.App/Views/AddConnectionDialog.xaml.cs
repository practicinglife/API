using System.Windows;
using MspTools.App.ViewModels;

namespace MspTools.App.Views;

public partial class AddConnectionDialog : Window
{
    private readonly NewConnectionFormViewModel _vm;

    public AddConnectionDialog(NewConnectionFormViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void AddConnection_Click(object sender, RoutedEventArgs e)
    {
        // Push PasswordBox values into the ViewModel (PasswordBox doesn't support two-way binding)
        _vm.PrivateKey = PrivateKeyBox.Password;
        _vm.Password = PasswordBox.Password;
        _vm.ClientSecret = ClientSecretBox.Password;
        _vm.BearerToken = BearerTokenBox.Password;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
