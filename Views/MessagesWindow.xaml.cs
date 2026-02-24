using System;
using System.Collections.Specialized;
using System.Linq;
using System.Media;
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
        private bool _stickToBottom = true;
        private bool _programmaticScroll;

        private DateTime _lastNotifyUtc = DateTime.MinValue;

        public MessagesWindow(TdLibService td, long chatId, string title)
        {
            InitializeComponent();

            _vm = new MessagesShellViewModel(td, chatId, title);
            DataContext = _vm;

            _vm.NewMessageArrived += Vm_NewMessageArrived;
            _vm.Messages.CollectionChanged += Messages_CollectionChanged;

            Loaded += async (_, __) =>
            {
                await _vm.LoadLatestMessagesAsync(80);

                _scroll = FindVisualChild<ScrollViewer>(MessagesList);
                if (_scroll != null)
                    _scroll.ScrollChanged += Scroll_ScrollChanged;

                BeginInitialFocus();
            };

            ContentRendered += (_, __) =>
            {
                if (!_initialFocusDone)
                    BeginInitialFocus();
            };

            Closed += (_, __) =>
            {
                try { _vm.NewMessageArrived -= Vm_NewMessageArrived; } catch { }
                try { _vm.Messages.CollectionChanged -= Messages_CollectionChanged; } catch { }
                try { _vm.Dispose(); } catch { }
            };
        }

        private void ReplyButton_Click(object sender, RoutedEventArgs e)
        {
            var msg = (sender as FrameworkElement)?.Tag as MessageDisplayItem;
            _vm.StartReply(msg);
            FocusComposer();
        }

        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.StartCompose();
            FocusComposer();
        }

        private void CancelReply_Click(object sender, RoutedEventArgs e)
        {
            _vm.CancelReply();
            FocusComposer();
        }

        private void FocusComposer()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try { ComposerBox.Focus(); Keyboard.Focus(ComposerBox); } catch { }
            }));
        }

        private void Vm_NewMessageArrived(MessageDisplayItem _)
        {
            if (!IsActive) return;

            var now = DateTime.UtcNow;
            if ((now - _lastNotifyUtc).TotalMilliseconds < 900) return;
            _lastNotifyUtc = now;

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try { SystemSounds.Asterisk.Play(); } catch { }
            }));
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_vm.Messages.Count == 0) return;

            bool shouldStick = _stickToBottom || _scroll == null;
            if (!shouldStick) return;

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    var last = _vm.Messages.Last();

                    _programmaticScroll = true;

                    MessagesList.UpdateLayout();
                    _scroll ??= FindVisualChild<ScrollViewer>(MessagesList);

                    if (_scroll != null) _scroll.ScrollToEnd();
                    else MessagesList.ScrollIntoView(last);

                    MessagesList.UpdateLayout();
                }
                catch { }
                finally
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() => _programmaticScroll = false));
                }
            }));
        }

        private async void Scroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scroll == null) return;

            if (!_programmaticScroll)
            {
                double bottomThreshold = 2.0;
                bool atBottom = _scroll.VerticalOffset >= (_scroll.ScrollableHeight - bottomThreshold);
                _stickToBottom = atBottom;
            }

            if (_scroll.VerticalOffset <= 0.0 && _vm.CanLoadOlder)
            {
                _stickToBottom = false;

                double oldExtent = _scroll.ExtentHeight;
                double oldOffset = _scroll.VerticalOffset;

                await _vm.LoadOlderMessagesAsync(30);

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (_scroll == null) return;

                    double newExtent = _scroll.ExtentHeight;
                    double delta = newExtent - oldExtent;

                    _programmaticScroll = true;
                    _scroll.ScrollToVerticalOffset(oldOffset + delta);
                    Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() => _programmaticScroll = false));
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
            if (_initialFocusDone) return;
            if (_vm.Messages.Count == 0) return;

            var last = _vm.Messages.Last();
            _vm.SelectedMessage = last;
            MessagesList.SelectedItem = last;

            try
            {
                _programmaticScroll = true;
                MessagesList.UpdateLayout();
                MessagesList.ScrollIntoView(last);
                MessagesList.UpdateLayout();
            }
            catch { }
            finally
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() => _programmaticScroll = false));
            }

            if (MessagesList.ItemContainerGenerator.ContainerFromItem(last) is FrameworkElement container)
            {
                try
                {
                    container.BringIntoView();
                    Keyboard.Focus(container);
                    container.Focus();
                    _initialFocusDone = true;
                    _stickToBottom = true;
                    return;
                }
                catch { }
            }

            attempts--;
            if (attempts <= 0)
            {
                try { Keyboard.Focus(MessagesList); MessagesList.Focus(); } catch { }
                _initialFocusDone = true;
                _stickToBottom = true;
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
                if (child is T typed) return typed;

                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        // Esc в списке сообщений -> закрыть окно
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            if (Keyboard.FocusedElement is TextBox)
                return;

            e.Handled = true;
            Close();
        }

        // Esc в редакторе:
        // - если ответ -> отмена ответа
        // - иначе -> скрыть редактор и вернуть фокус в список
        private void ComposerBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                _stickToBottom = true;

                _ = SendAndScrollAsync();
                return;
            }

            if (e.Key != Key.Escape)
                return;

            e.Handled = true;

            if (_vm.IsReplyMode)
            {
                _vm.CancelReply();
                return;
            }

            _vm.HideComposer();

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try { Keyboard.Focus(MessagesList); MessagesList.Focus(); } catch { }
            }));
        }

        private async System.Threading.Tasks.Task SendAndScrollAsync()
        {
            bool ok = false;
            try { ok = await _vm.SendCurrentAsync(); } catch { ok = false; }

            if (!ok) return;

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    _programmaticScroll = true;
                    MessagesList.UpdateLayout();
                    _scroll ??= FindVisualChild<ScrollViewer>(MessagesList);
                    _scroll?.ScrollToEnd();
                }
                catch { }
                finally
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() => _programmaticScroll = false));
                }
            }));
        }
    }
}