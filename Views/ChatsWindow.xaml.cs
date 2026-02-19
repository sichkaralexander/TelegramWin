using System.Windows;
using TelegramWin.ViewModels;
using TelegramWin.Services;

namespace TelegramWin.Views
{
    public partial class ChatsWindow : Window
    {
        private readonly ChatsViewModel _vm;

        public ChatsWindow(TdLibService td)
        {
            InitializeComponent();
            _vm = new ChatsViewModel(td);
            DataContext = _vm;

            Loaded += async (_, __) =>
            {
                try { ChatsList.Focus(); } catch { }
                await _vm.LoadAsync();
            };
        }
    }
}
