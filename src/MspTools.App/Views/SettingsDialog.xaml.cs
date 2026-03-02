using System.Windows;
using MspTools.App.Data;

namespace MspTools.App.Views;

public partial class SettingsDialog : Window
{
    public string ConnectionString => ConnectionStringBox.Text;

    public SettingsDialog(string currentConnectionString)
    {
        InitializeComponent();
        ConnectionStringBox.Text = currentConnectionString;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Testing…";
        try
        {
            var repo = new LocalRepository(ConnectionStringBox.Text);
            await repo.EnsureSchemaAsync();
            StatusText.Text = "✔ Connection successful. Schema is ready.";
            StatusText.Foreground = System.Windows.Media.Brushes.Green;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✖ {ex.Message}";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
