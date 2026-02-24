using System;
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (!IsFocusInside(ChatsList))
                return;

            e.Handled = true;
            OpenSelectedChat_Debug();
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
                OpenSelectedChat_Debug();
            }
        }

        private void ChatsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedChat_Debug();
        }

        private void OpenSelectedChat_Debug()
        {
            try
            {
                var item = ChatsList.SelectedItem;

                if (item == null)
                {
                    MessageBox.Show("SelectedItem = null. Выдели чат стрелками и попробуй Enter.", "DEBUG",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int selectedIndex = ChatsList.SelectedIndex;

                long chatId = TryGetLong(item, "ChatId", "Id", "chat_id", "Chat_id");
                string title = TryGetString(item, "Title", "Name", "ChatTitle", "DisplayTitle") ?? "Чат";

                if (chatId == 0)
                {
                    MessageBox.Show("chatId = 0 (не удалось получить Id чата из объекта). Сообщи мне этот текст.",
                        "DEBUG", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                w.Show();
                w.Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("OpenSelectedChat exception:\r\n" + ex, "DEBUG",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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