using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using TelegramWin.Services;
using TelegramWin.ViewModels;

namespace TelegramWin.Views
{
    public partial class ChatsWindow : Window
    {
        private readonly TdLibService _td;
        private readonly ChatsViewModel _vm;

        public ChatsWindow(TdLibService td)
        {
            InitializeComponent();

            _td = td;
            _vm = new ChatsViewModel(td);
            DataContext = _vm;

            Loaded += async (_, __) =>
            {
                try { ChatsList.Focus(); } catch { }
                await _vm.LoadAsync();
            };
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
            var item = ChatsList.SelectedItem;
            if (item == null)
                return;

            // Запоминаем текущее выделение, чтобы после закрытия MessagesWindow
            // фокус/выделение НЕ прыгали на первый чат.
            int selectedIndex = ChatsList.SelectedIndex;

            long chatId = TryGetLong(item, "ChatId", "Id", "chat_id", "Chat_id");
            string title = TryGetString(item, "Title", "Name", "ChatTitle", "DisplayTitle") ?? "Чат";
            if (chatId == 0)
                title = $"{title} (chatId не найден)";

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

            w.Show();
            w.Activate();
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
