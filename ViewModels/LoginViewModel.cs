using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TelegramWin.Services;

namespace TelegramWin.ViewModels
{
    public sealed class LoginViewModel : INotifyPropertyChanged
    {
        private readonly TdLibService _td;
        private AuthStep _step = AuthStep.Internal;
        private string _phone = "";
        private string _code = "";
        private string _password = "";
        private string _status = "Запуск…";
        private bool _isBusy;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LoginViewModel(TdLibService td)
        {
            _td = td;

            _td.StatusChanged += s => StatusText = s;
            _td.AuthStepChanged += step => Step = step;
        }

        public AuthStep Step
        {
            get => _step;
            private set { _step = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPhoneStep)); OnPropertyChanged(nameof(IsCodeStep)); OnPropertyChanged(nameof(IsPasswordStep)); }
        }

        public bool IsPhoneStep => Step == AuthStep.Phone;
        public bool IsCodeStep => Step == AuthStep.Code;
        public bool IsPasswordStep => Step == AuthStep.Password;

        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        public string Code
        {
            get => _code;
            set { _code = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); }
        }

        public async Task SendPhoneAsync()
        {
            IsBusy = true;
            try { await _td.SendPhoneAsync(Phone); }
            finally { IsBusy = false; }
        }

        public async Task SendCodeAsync()
        {
            IsBusy = true;
            try { await _td.SendCodeAsync(Code); }
            finally { IsBusy = false; }
        }

        public async Task SendPasswordAsync()
        {
            IsBusy = true;
            try { await _td.SendPasswordAsync(Password); }
            finally { IsBusy = false; }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
