using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TelegramWin.ViewModels;

namespace TelegramWin.Views
{
    public partial class ComposerWindow : Window
    {
        private MessagesShellViewModel? Vm => DataContext as MessagesShellViewModel;

        public ComposerWindow()
        {
            InitializeComponent();

            Loaded += (_, __) =>
            {
                // Фокус железно в поле ввода
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        ComposerBox.Focus();
                        Keyboard.Focus(ComposerBox);
                        var len = ComposerBox.Text?.Length ?? 0;
                        ComposerBox.Select(len, 0);
                    }
                    catch { }
                }, DispatcherPriority.Loaded);
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Отмена — просто закрыть окно редактора (сообщения не закрываем)
            Close();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await TrySendAndCloseAsync();
        }

        private async void ComposerBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Shift+Enter оставляем как новая строка (TextBox сам вставит перенос)
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                return;

            // Enter/Return — отправить
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                e.Handled = true;
                await TrySendAndCloseAsync();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Esc — отмена (IsCancel тоже есть, но на всякий случай)
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private async Task TrySendAndCloseAsync()
        {
            if (Vm == null) return;

            var ok = await Vm.SendCurrentAsync();
            if (!ok)
            {
                // Не отправилось — остаёмся в окне, чтобы пользователь не потерял текст
                return;
            }

            // По твоему ТЗ: после отправки окно редактора закрывается
            Close();
        }
    }
}