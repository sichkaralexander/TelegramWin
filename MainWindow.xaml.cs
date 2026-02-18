using System;
using System.IO;
using System.Windows;
using TelegramWin.Services;
using TdLib;

namespace TelegramWin
{
    public partial class MainWindow : Window
    {
        private readonly TdLibService _td = new TdLibService();
        private AppConfig _cfg = new AppConfig();

        public MainWindow()
        {
            InitializeComponent();
            _td.AuthorizationChanged += OnAuthChanged;
            try { _cfg = AppConfig.Load(); } catch { }
            StatusBox.Text = "Заполните ApiId и ApiHash в appsettings.json.";
        }

        private async void SendPhone_Click(object sender, RoutedEventArgs e)
        {
            await _td.StartAsync(_cfg.ApiId, _cfg.ApiHash, "tdlib");
            await _td.SendPhoneAsync(PhoneBox.Text);
        }

        private async void SendCode_Click(object sender, RoutedEventArgs e)
        {
            await _td.SendCodeAsync(CodeBox.Text);
        }

        private async void SendPassword_Click(object sender, RoutedEventArgs e)
        {
            await _td.SendPasswordAsync(PwdBox.Password);
        }

        private void OnAuthChanged(TdApi.UpdateAuthorizationState auth)
        {
            Dispatcher.Invoke(() =>
            {
                StatusBox.Text = auth.AuthorizationState.GetType().Name;
            });
        }
    }
}
