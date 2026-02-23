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
                Chats.Clear();

                // У разных версий TdLib.Api/TDLib сигнатуры GetChats отличаются.
                // Делаем "мягкую" пагинацию через reflection:
                // - если есть OffsetChatId + OffsetOrder И у Chat есть Order → грузим страницами
                // - иначе → один вызов GetChats с большим Limit
                const int pageSize = 100;
                const int maxTotal = 500;

                long offsetChatId = 0;
                long offsetOrder = long.MaxValue;

                var reqType = typeof(TdApi.GetChats);
                var pOffsetChatId = reqType.GetProperty("OffsetChatId");
                var pOffsetOrder = reqType.GetProperty("OffsetOrder");

                var chatType = typeof(TdApi.Chat);
                var pChatOrder = chatType.GetProperty("Order");

                bool canPage = pOffsetChatId != null && pOffsetOrder != null && pChatOrder != null;

                int total = 0;

                while (total < maxTotal)
                {
                    var req = new TdApi.GetChats { Limit = canPage ? pageSize : maxTotal };

                    if (canPage)
                    {
                        try { pOffsetChatId!.SetValue(req, offsetChatId); } catch { }
                        try { pOffsetOrder!.SetValue(req, offsetOrder); } catch { }
                    }

                    var page = await _td.ExecuteAsync(req);

                    if (page?.ChatIds == null || page.ChatIds.Length == 0)
                        break;

                    foreach (var id in page.ChatIds)
                    {
                        if (Chats.Any(c => c.Id == id))
                            continue;

                        Chats.Add(new ChatItem(id));
                        total++;
                        if (total >= maxTotal)
                            break;
                    }

                    if (!canPage)
                        break;

                    // следующий оффсет — по последнему чату страницы
                    var lastId = page.ChatIds.Last();
                    var lastChat = await _td.ExecuteAsync(new TdApi.GetChat { ChatId = lastId });

                    offsetChatId = lastId;

                    // Order читаем через reflection (чтобы не зависеть от конкретной версии API)
                    try
                    {
                        var val = pChatOrder!.GetValue(lastChat);
                        offsetOrder = val == null ? 0 : Convert.ToInt64(val);
                    }
                    catch
                    {
                        offsetOrder = 0;
                    }

                    if (offsetOrder == 0)
                        break;
                }

                // Заполняем Title и LastMessage
                foreach (var item in Chats)
                {
                    try
                    {
                        var chat = await _td.ExecuteAsync(new TdApi.GetChat { ChatId = item.Id });
                        item.Title = chat.Title ?? "(без названия)";
                        item.LastMessage = MessagePreviewFormatter.FromMessage(chat.LastMessage);
                    }
                    catch { }
                }

                Status = $"Чаты загружены: {Chats.Count}";
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
                    item.LastMessage = MessagePreviewFormatter.FromMessage(lastMsgObj);
            }
            catch { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
