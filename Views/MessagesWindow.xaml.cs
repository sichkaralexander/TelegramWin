using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TelegramWin.Services;
using TelegramWin.ViewModels;

namespace TelegramWin.Views
{
    public partial class MessagesWindow : Window
    {
        private MessagesShellViewModel? Vm => DataContext as MessagesShellViewModel;
        private readonly Announcer _announcer;

        public MessagesWindow(TdLibService td, long chatId, string title)
        {
            InitializeComponent();

            DataContext = new MessagesShellViewModel(td, chatId, title);
            Title = title;

            _announcer = new Announcer(this, liveRegionName: "LiveStatus");

            Loaded += MessagesWindow_Loaded;
            Closed += MessagesWindow_Closed;
        }

        public MessagesWindow()
        {
            InitializeComponent();
            _announcer = new Announcer(this, liveRegionName: "LiveStatus");
            Loaded += MessagesWindow_Loaded;
            Closed += MessagesWindow_Closed;
        }

        private void MessagesWindow_Closed(object? sender, EventArgs e)
        {
            if (Vm != null)
            {
                Vm.NewMessageArrived -= Vm_NewMessageArrived;
            }
        }

        private async void MessagesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            Vm.NewMessageArrived -= Vm_NewMessageArrived;
            Vm.NewMessageArrived += Vm_NewMessageArrived;

            try
            {
                await Vm.LoadLatestMessagesAsync(60);
            }
            catch { }

            _announcer.Say($"Чат открыт: {Title}");

            // Ключевое: поставить фокус на ПОСЛЕДНЕЕ сообщение, чтобы JAWS сразу прочитал.
            await FocusLastMessageWithRetriesAsync();
        }

        private void Vm_NewMessageArrived(MessageDisplayItem item)
        {
            if (item == null)
                return;

            // Без прыгающего фокуса: только озвучка события.
            _announcer.Say("Новое сообщение. " + item.AccessibleText);
        }

        private void ReplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            if (Vm.SelectedMessage == null) return;

            Vm.StartReply(Vm.SelectedMessage);
            ShowComposerAndFocus();
        }

        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            Vm.StartCompose();
            ShowComposerAndFocus();
        }

        private void CancelReply_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            Vm.CancelReply();
            ShowComposerAndFocus();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendAndHideComposerAsync();
        }

        private async void ComposerBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Vm == null) return;

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }

            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    return;

                e.Handled = true;
                await SendAndHideComposerAsync();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private async Task SendAndHideComposerAsync()
        {
            if (Vm == null) return;

            var ok = await Vm.SendCurrentAsync();
            if (!ok)
            {
                _announcer.Say("Не удалось отправить сообщение");
                ShowComposerAndFocus();
                return;
            }

            _announcer.Say("Сообщение отправлено");

            // после отправки — закрываем редактор
            Vm.HideComposer();

            // и возвращаемся к последнему сообщению (вниз), чтобы навигация стрелками была логичной
            await FocusLastMessageWithRetriesAsync();
        }

        private void ShowComposerAndFocus()
        {
            if (Vm == null) return;

            Vm.IsComposerVisible = true;

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
        }

        private async Task FocusLastMessageWithRetriesAsync()
        {
            // 3 попытки: сразу / через 80мс / через 180мс
            await FocusLastMessageOnceAsync();
            await Task.Delay(80);
            await FocusLastMessageOnceAsync();
            await Task.Delay(180);
            await FocusLastMessageOnceAsync();
        }

        private async Task FocusLastMessageOnceAsync()
        {
            if (Vm == null) return;
            if (MessagesList == null) return;

            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var count = Vm.Messages?.Count ?? 0;
                    if (count <= 0) return;

                    int lastIndex = count - 1;

                    // Важно: меняем SelectedItem -> JAWS чаще начинает читать
                    var lastItem = Vm.Messages[lastIndex];
                    Vm.SelectedMessage = lastItem;

                    MessagesList.SelectedIndex = lastIndex;
                    MessagesList.ScrollIntoView(lastItem);
                    MessagesList.UpdateLayout();

                    // Фокус на КОНТЕЙНЕРЕ (ListBoxItem), а не на ListBox.
                    if (MessagesList.ItemContainerGenerator.ContainerFromIndex(lastIndex) is ListBoxItem lbi)
                    {
                        lbi.Focus();
                        Keyboard.Focus(lbi);
                    }
                    else
                    {
                        // fallback
                        MessagesList.Focus();
                        Keyboard.Focus(MessagesList);
                    }
                }
                catch { }
            }, DispatcherPriority.Background);
        }
    }
}
