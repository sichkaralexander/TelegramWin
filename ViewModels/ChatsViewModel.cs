using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TdLib;
using TelegramWin.Services;

namespace TelegramWin.ViewModels
{
    public sealed class ChatItem : INotifyPropertyChanged
    {
        public long Id { get; }
        private string _title = "";
        private string _last = "";

        public string Title { get => _title; set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(AccessibleText)); } }
        public string LastMessage { get => _last; set { _last = value; OnPropertyChanged(); OnPropertyChanged(nameof(AccessibleText)); } }

        public string AccessibleText => string.IsNullOrWhiteSpace(LastMessage) ? Title : $"{Title}. {LastMessage}";

        public ChatItem(long id) => Id = id;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public sealed class ChatsViewModel : INotifyPropertyChanged
    {
        private readonly TdLibService _td;
        public ObservableCollection<ChatItem> Chats { get; } = new();

        private string _status = "Загрузка чатов…";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        public ChatsViewModel(TdLibService td)
        {
            _td = td;
            _td.UpdateReceivedPublic += OnUpdate;
        }

        public async Task LoadAsync()
        {
            try
            {
                Status = "Получаю список чатов…";
                var chats = await _td.ExecuteAsync(new TdApi.GetChats { Limit = 50 });

                Chats.Clear();
                foreach (var id in chats.ChatIds)
                    Chats.Add(new ChatItem(id));

                foreach (var item in Chats)
                {
                    try
                    {
                        var chat = await _td.ExecuteAsync(new TdApi.GetChat { ChatId = item.Id });
                        item.Title = chat.Title ?? "(без названия)";
                        item.LastMessage = ExtractText(chat.LastMessage);
                    }
                    catch { }
                }

                Status = "Чаты загружены";
            }
            catch (Exception ex)
            {
                Status = "Ошибка загрузки чатов: " + ex.Message;
            }
        }

        private void OnUpdate(TdApi.Update update)
        {
            try
            {
                // ✅ НЕ используем TdApi.UpdateChatLastMessage напрямую (его может не быть).
                // Вместо этого определяем по имени типа и читаем свойства через reflection.
                if (!string.Equals(update.GetType().Name, "UpdateChatLastMessage", StringComparison.Ordinal))
                    return;

                var t = update.GetType();

                var chatIdProp = t.GetProperty("ChatId", BindingFlags.Public | BindingFlags.Instance);
                var lastMsgProp = t.GetProperty("LastMessage", BindingFlags.Public | BindingFlags.Instance);

                if (chatIdProp == null || lastMsgProp == null)
                    return;

                var chatIdObj = chatIdProp.GetValue(update);
                if (chatIdObj == null) return;

                long chatId;
                try { chatId = Convert.ToInt64(chatIdObj); }
                catch { return; }

                var lastMsgObj = lastMsgProp.GetValue(update) as TdApi.Message;

                var item = Chats.FirstOrDefault(x => x.Id == chatId);
                if (item != null)
                    item.LastMessage = ExtractText(lastMsgObj);
            }
            catch { }
        }

        private static string ExtractText(TdApi.Message? msg)
        {
            if (msg == null) return "";
            try
            {
                if (msg.Content is TdApi.MessageContent.MessageText mt)
                    return mt.Text?.Text ?? "";
            }
            catch { }
            return "";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
