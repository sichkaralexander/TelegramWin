using System.Windows;
using System.Windows.Automation;

namespace TelegramWin
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SendCode_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = $"Код отправлен на номер {PhoneBox.Text}";
            AutomationProperties.SetLiveSetting(StatusText, AutomationLiveSetting.Assertive);
        }

        private void ConfirmCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CodeBox.Text))
            {
                StatusText.Text = "Введите код подтверждения.";
            }
            else
            {
                StatusText.Text = "Код подтверждён (демо режим).";
            }

            AutomationProperties.SetLiveSetting(StatusText, AutomationLiveSetting.Assertive);
        }
    }
}
