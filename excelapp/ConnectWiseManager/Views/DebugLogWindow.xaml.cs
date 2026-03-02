using System;
using System.Text;
using System.Windows;

namespace ConnectWiseManager.Views
{
    public partial class DebugLogWindow : Window
    {
        private readonly StringBuilder _buffer = new StringBuilder(4096);
        public DebugLogWindow()
        {
            InitializeComponent();
        }
        public void AppendLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _buffer.AppendLine(line);
            if (LogTextBox != null)
            {
                LogTextBox.Text = _buffer.ToString();
                LogTextBox.ScrollToEnd();
            }
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _buffer.Clear();
            LogTextBox.Clear();
        }
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(LogTextBox.Text ?? string.Empty); }
            catch { }
        }
    }
}