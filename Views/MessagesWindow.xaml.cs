using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TelegramWin.Services;
using TelegramWin.ViewModels;

namespace TelegramWin.Views
{
    public partial class MessagesWindow : Window
    {
        private readonly MessagesShellViewModel _vm;

        public MessagesWindow(TdLibService td, long chatId, string title)
        {
            InitializeComponent();

            _vm = new MessagesShellViewModel(td, chatId, title);
            DataContext = _vm;

            Loaded += (_, __) =>
            {
                _vm.LoadTestMessages();

                // Важно: даём разметке построиться и фокусим именно TextBlock последнего сообщения.
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FocusLastMessageTextForJaws));
            };
        }

        private void FocusLastMessageTextForJaws()
        {
            if (_vm.Messages.Count == 0)
                return;

            var last = _vm.Messages.Last();
            _vm.SelectedMessage = last;
            MessagesList.SelectedItem = last;

            try
            {
                MessagesList.UpdateLayout();
                MessagesList.ScrollIntoView(last);
                MessagesList.UpdateLayout();

                if (MessagesList.ItemContainerGenerator.ContainerFromItem(last) is FrameworkElement container)
                {
                    container.BringIntoView();
                    container.UpdateLayout();

                    // Находим TextBlock с текстом сообщения и ставим фокус на него.
                    var tb = FindFirstFocusableTextBlock(container);
                    if (tb != null)
                    {
                        tb.BringIntoView();
                        Keyboard.Focus(tb);
                        tb.Focus();
                        return;
                    }

                    // Фоллбек: фокус на контейнер.
                    Keyboard.Focus(container);
                    container.Focus();
                }
                else
                {
                    MessagesList.Focus();
                }
            }
            catch
            {
                try { MessagesList.Focus(); } catch { }
            }
        }

        private static System.Windows.Controls.TextBlock? FindFirstFocusableTextBlock(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is System.Windows.Controls.TextBlock tb && tb.Focusable)
                    return tb;

                var found = FindFirstFocusableTextBlock(child);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }
    }
}
