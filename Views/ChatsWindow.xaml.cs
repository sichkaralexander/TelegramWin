using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TelegramWin.Services;
using TelegramWin.ViewModels;

namespace TelegramWin.Views
{
    public partial class ChatsWindow : Window
    {
        private readonly TdLibService _td;
        private readonly ChatsViewModel _vm;
        private readonly Announcer _announcer;

        public ChatsWindow(TdLibService td)
        {
            InitializeComponent();

            _td = td;
            _vm = new ChatsViewModel(td);
            DataContext = _vm;

            _announcer = new Announcer(this, liveRegionName: "LiveStatus", statusName: "StatusTextBlock");

            Loaded += async (_, __) =>
            {
                try { ChatsList.Focus(); } catch { }
                _announcer.Say("Окно чатов открыто");
                await _vm.LoadAsync();
            };

            _vm.PropertyChanged += VmOnPropertyChanged;
        }

        protected override void OnClosed(EventArgs e)
        {
            _vm.PropertyChanged -= VmOnPropertyChanged;
            base.OnClosed(e);
        }

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatsViewModel.Status))
            {
                _announcer.Say(_vm.Status);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (!IsFocusInside(ChatsList))
                return;

            e.Handled = true;
            OpenSelectedChat();
        }

        private static bool IsFocusInside(DependencyObject root)
        {
            try
            {
                var focused = Keyboard.FocusedElement as DependencyObject;
                if (focused == null) return false;

                var cur = focused;
                while (cur != null)
                {
                    if (ReferenceEquals(cur, root))
                        return true;

                    if (cur is Visual || cur is System.Windows.Media.Media3D.Visual3D)
                        cur = VisualTreeHelper.GetParent(cur);
                    else
                        cur = LogicalTreeHelper.GetParent(cur);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private void ChatsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenSelectedChat();
            }
        }

        private void ChatsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedChat();
        }

        private void OpenSelectedChat()
        {
            try
            {
                var item = ChatsList.SelectedItem;

                if (item == null)
                {
                    _vm.Status = "Чат не выбран. Выберите чат стрелками и нажмите Enter.";
                    _announcer.Say(_vm.Status);
                    return;
                }

                int selectedIndex = ChatsList.SelectedIndex;

                long chatId = TryGetLong(item, "ChatId", "Id", "chat_id", "Chat_id");
                string title = TryGetString(item, "Title", "Name", "ChatTitle", "DisplayTitle") ?? "Чат";

                if (chatId == 0)
                {
                    _vm.Status = "Не удалось открыть чат: отсутствует идентификатор.";
                    _announcer.Say(_vm.Status);
                    return;
                }

                var w = new MessagesWindow(_td, chatId, title)
                {
                    Owner = this
                };

                w.Closed += (_, __) =>
                {
                    try
                    {
                        Activate();
                        ChatsList.Focus();
                        if (selectedIndex >= 0 && selectedIndex < ChatsList.Items.Count)
                        {
                            ChatsList.SelectedIndex = selectedIndex;
                            ChatsList.ScrollIntoView(ChatsList.SelectedItem);
                        }
                    }
                    catch { }
                };

                _announcer.Say($"Открываю чат: {title}");
                w.Show();
                w.Activate();
            }
            catch
            {
                _vm.Status = "Ошибка при открытии чата.";
                _announcer.Say(_vm.Status);
            }
        }

        private static long TryGetLong(object obj, params string[] names)
        {
            foreach (var name in names)
            {
                var p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (p == null) continue;

                try
                {
                    var v = p.GetValue(obj);
                    if (v == null) continue;

                    if (v is long l) return l;
                    if (v is int i) return i;
                    if (v is ulong ul && ul <= long.MaxValue) return (long)ul;

                    if (long.TryParse(v.ToString(), out var parsed))
                        return parsed;
                }
                catch { }
            }
            return 0;
        }

        private static string? TryGetString(object obj, params string[] names)
        {
            foreach (var name in names)
            {
                var p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (p == null) continue;

                try
                {
                    var v = p.GetValue(obj);
                    var s = v?.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
                catch { }
            }
            return null;
        }
    }
}
