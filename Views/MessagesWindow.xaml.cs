using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private bool _initialFocusDone;

        private ScrollViewer? _scroll;

        public MessagesWindow(TdLibService td, long chatId, string title)
        {
            InitializeComponent();

            _vm = new MessagesShellViewModel(td, chatId, title);
            DataContext = _vm;

            Loaded += async (_, __) =>
            {
                await _vm.LoadLatestMessagesAsync(80);

                // Подключаем ScrollViewer после того как визуальное дерево есть
                _scroll = FindVisualChild<ScrollViewer>(MessagesList);
                if (_scroll != null)
                    _scroll.ScrollChanged += Scroll_ScrollChanged;

                // Фокус на последний элемент — с ретраями
                BeginInitialFocus();
            };

            ContentRendered += (_, __) =>
            {
                if (!_initialFocusDone)
                    BeginInitialFocus();
            };
        }

        private async void Scroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scroll == null)
                return;

            // Дошли до самого верха -> дозагрузить ещё 30 старых сообщений
            // Используем небольшой порог, чтобы срабатывало стабильно
            if (_scroll.VerticalOffset <= 0.0 && _vm.CanLoadOlder)
            {
                // Сохраняем позицию, чтобы после вставки сверху "не прыгало"
                double oldExtent = _scroll.ExtentHeight;
                double oldOffset = _scroll.VerticalOffset;

                await _vm.LoadOlderMessagesAsync(30);

                // Ждём перерасчёт размеров и восстанавливаем позицию
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (_scroll == null) return;
                    double newExtent = _scroll.ExtentHeight;
                    double delta = newExtent - oldExtent;
                    _scroll.ScrollToVerticalOffset(oldOffset + delta);
                }));
            }
        }

        private void BeginInitialFocus()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                TryFocusLastMessageWithRetries(attempts: 10, delayMs: 80);
            }));
        }

        private void TryFocusLastMessageWithRetries(int attempts, int delayMs)
        {
            if (_initialFocusDone)
                return;

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
            }
            catch { }

            if (MessagesList.ItemContainerGenerator.ContainerFromItem(last) is FrameworkElement container)
            {
                try
                {
                    container.BringIntoView();
                    Keyboard.Focus(container);
                    container.Focus();
                    _initialFocusDone = true;
                    return;
                }
                catch { }
            }

            attempts--;
            if (attempts <= 0)
            {
                try
                {
                    Keyboard.Focus(MessagesList);
                    MessagesList.Focus();
                }
                catch { }
                _initialFocusDone = true;
                return;
            }

            var timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(delayMs)
            };
            timer.Tick += (_, __) =>
            {
                timer.Stop();
                TryFocusLastMessageWithRetries(attempts, delayMs);
            };
            timer.Start();
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                    return typed;

                var found = FindVisualChild<T>(child);
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
