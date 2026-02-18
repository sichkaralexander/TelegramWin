using System.Windows;
using TelegramWin.Views;

namespace TelegramWin
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenLogin_Click(object sender, RoutedEventArgs e)
        {
            var w = new LoginWindow();
            w.Show();
            Close();
        }
    }
}
