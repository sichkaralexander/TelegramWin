using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TdLib;
using TelegramWin.Services;

namespace TelegramWin.ViewModels
{
    public sealed class MessagesShellViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly TdLibService _td;
        private readonly long _chatId;
        private readonly SynchronizationContext _ui;

        private string _title;
        private string _status = "";
        private MessageDisplayItem? _selectedMessage;

        private readonly Dictionary<long, string> _userCache = new();

        private readonly HashSet<long> _loadedMessageIds = new();
        private long _oldestMessageIdLoaded;
        private bool _hasMoreOlder = true;
        private int _isLoadingOlderFlag = 0;
        private readonly SemaphoreSlim _olderGate = new(1, 1);

        private bool _disposed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<MessageDisplayItem> Messages { get; } = new();

        public event Action<MessageDisplayItem>? NewMessageArrived;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public MessageDisplayItem? SelectedMessage
        {
            get => _selectedMessage;
            set { _selectedMessage = value; OnPropertyChanged(); }
        }

        public bool IsLoadingOlder => Volatile.Read(ref _isLoadingOlderFlag) == 1;
        public bool CanLoadOlder => _hasMoreOlder && !IsLoadingOlder && _oldestMessageIdLoaded != 0;

        public MessagesShellViewModel(TdLibService td, long chatId, string title)
        {
            _td = td;
            _chatId = chatId;
            _title = title;

            _ui = SynchronizationContext.Current ?? new SynchronizationContext();

            _td.UpdateReceivedPublic += OnUpdate;
        }

        public async Task LoadLatestMessagesAsync(int limit)
        {
            Status = "Загрузка сообщений…";

            _loadedMessageIds.Clear();
            _oldestMessageIdLoaded = 0;
            _hasMoreOlder = true;

            await EnsureChatOpenedAsync();

            TdApi.Messages? history = null;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                history = await _td.ExecuteAsync(new TdApi.GetChatHistory
                {
                    ChatId = _chatId,
                    FromMessageId = 0,
                    Offset = 0,
                    Limit = limit,
                    OnlyLocal = false
                });

                var count = history?.Messages_?.Length ?? 0;
                if (count >= 2)
                    break;

                await Task.Delay(180);
            }

            if (history?.Messages_ == null || history.Messages_.Length == 0)
            {
                _ui.Post(_ => Messages.Clear(), null);
                Status = "";
                return;
            }

            Array.Reverse(history.Messages_);

            var prepared = new List<MessageDisplayItem>(history.Messages_.Length);

            foreach (var m in history.Messages_)
            {
                if (m.ChatId != _chatId) continue;
                if (m.Id != 0 && _loadedMessageIds.Contains(m.Id)) continue;

                var item = await BuildDisplayItemAsync(m);
                prepared.Add(item);

                if (m.Id != 0) _loadedMessageIds.Add(m.Id);
            }

            _oldestMessageIdLoaded = history.Messages_[0].Id;

            _ui.Post(_ =>
            {
                Messages.Clear();
                foreach (var item in prepared)
                    Messages.Add(item);
            }, null);

            Status = "";
        }

        public async Task LoadOlderMessagesAsync(int limit)
        {
            if (!_hasMoreOlder) return;
            if (_oldestMessageIdLoaded == 0) return;

            if (!await _olderGate.WaitAsync(0))
                return;

            try
            {
                Interlocked.Exchange(ref _isLoadingOlderFlag, 1);

                var history = await _td.ExecuteAsync(new TdApi.GetChatHistory
                {
                    ChatId = _chatId,
                    FromMessageId = _oldestMessageIdLoaded,
                    Offset = -1,
                    Limit = limit,
                    OnlyLocal = false
                });

                if (history?.Messages_ == null || history.Messages_.Length == 0)
                {
                    _hasMoreOlder = false;
                    return;
                }

                Array.Reverse(history.Messages_);

                var prepared = new List<MessageDisplayItem>(history.Messages_.Length);

                foreach (var m in history.Messages_)
                {
                    if (m.ChatId != _chatId) continue;
                    if (m.Id != 0 && _loadedMessageIds.Contains(m.Id)) continue;

                    var item = await BuildDisplayItemAsync(m);
                    prepared.Add(item);

                    if (m.Id != 0) _loadedMessageIds.Add(m.Id);
                }

                if (prepared.Count == 0)
                    return;

                _oldestMessageIdLoaded = history.Messages_[0].Id;

                _ui.Post(_ =>
                {
                    for (int i = prepared.Count - 1; i >= 0; i--)
                        Messages.Insert(0, prepared[i]);
                }, null);
            }
            finally
            {
                Interlocked.Exchange(ref _isLoadingOlderFlag, 0);
                _olderGate.Release();
            }
        }

        private async Task EnsureChatOpenedAsync()
        {
            try { await _td.ExecuteAsync(new TdApi.OpenChat { ChatId = _chatId }); }
            catch { }
        }

        public async Task CloseChatAsync()
        {
            try { await _td.ExecuteAsync(new TdApi.CloseChat { ChatId = _chatId }); }
            catch { }
        }

        private async Task<MessageDisplayItem> BuildDisplayItemAsync(TdApi.Message m)
        {
            string text = ExtractText(m);
            var dt = DateTimeOffset.FromUnixTimeSeconds(m.Date).ToLocalTime().DateTime;
            string author = await ResolveAuthor(m.SenderId);

            return new MessageDisplayItem(
                text,
                $"{author} {dt:dd.MM.yyyy HH:mm}"
            );
        }

        private void OnUpdate(TdApi.Update update)
        {
            try
            {
                if (_disposed) return;

                if (!string.Equals(update.GetType().Name, "UpdateNewMessage", StringComparison.Ordinal))
                    return;

                var t = update.GetType();
                var msgProp = t.GetProperty("Message", BindingFlags.Public | BindingFlags.Instance);
                if (msgProp == null)
                    return;

                if (msgProp.GetValue(update) is not TdApi.Message msg)
                    return;

                if (msg.ChatId != _chatId)
                    return;

                if (msg.Id != 0 && _loadedMessageIds.Contains(msg.Id))
                    return;

                if (msg.Id != 0)
                    _loadedMessageIds.Add(msg.Id);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var item = await BuildDisplayItemAsync(msg);

                        _ui.Post(_ =>
                        {
                            Messages.Add(item);
                            try { NewMessageArrived?.Invoke(item); } catch { }
                        }, null);
                    }
                    catch { }
                });
            }
            catch { }
        }

        private string ExtractText(TdApi.Message m)
        {
            var s = MessagePreviewFormatter.FromMessage(m);
            return string.IsNullOrWhiteSpace(s) ? "(пусто)" : s;
        }

        private async Task<string> ResolveAuthor(TdApi.MessageSender sender)
        {
            if (sender is TdApi.MessageSender.MessageSenderUser su)
            {
                if (_userCache.TryGetValue(su.UserId, out var cached))
                    return cached;

                var user = await _td.ExecuteAsync(new TdApi.GetUser { UserId = su.UserId });

                string name = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(name))
                    name = $"User {user.Id}";

                _userCache[su.UserId] = name;
                return name;
            }

            return "";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _td.UpdateReceivedPublic -= OnUpdate;

            _ = Task.Run(async () =>
            {
                try { await CloseChatAsync(); } catch { }
            });
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
