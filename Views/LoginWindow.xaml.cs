using System.Windows;
using TelegramWin.Services;
using TelegramWin.ViewModels;

namespace TelegramWin.Views
{
    public partial class LoginWindow : Window
    {
        private readonly TdLibService _td;
        private readonly Announcer _announcer;
        private readonly LoginViewModel _vm;

        public LoginWindow()
        {
            InitializeComponent();

            // ✅ Берём один общий TdLibService, созданный в App.xaml.cs
            _td = App.TdLib;

            _announcer = new Announcer(this);

            _vm = new LoginViewModel(_td);
            DataContext = _vm;

            _td.StatusChanged += s => _announcer.Say(s);
            _td.AuthStepChanged += step => FocusForStep(step);

            Loaded += (_, _) =>
            {
                PhoneBox?.Focus();
                _announcer.Say("Окно входа. Введите номер телефона.");
            };
        }

        private void FocusForStep(AuthStep step)
        {
            Dispatcher.InvokeAsync(() =>
            {
                switch (step)
                {
                    case AuthStep.Phone:
                        PhoneBox?.Focus();
                        break;
                    case AuthStep.Code:
                        CodeBox?.Focus();
                        break;
                    case AuthStep.Password:
                        PasswordBox?.Focus();
                        break;
                }
            });
        }

        private async void SendPhone_Click(object sender, RoutedEventArgs e) => await _vm.SendPhoneAsync();
        private async void SendCode_Click(object sender, RoutedEventArgs e) => await _vm.SendCodeAsync();

        private async void SendPassword_Click(object sender, RoutedEventArgs e)
        {
            _vm.Password = PasswordBox?.Password ?? "";
            await _vm.SendPasswordAsync();
        }
    }
}
