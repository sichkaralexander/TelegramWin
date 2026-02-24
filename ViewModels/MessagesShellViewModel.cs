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

        // ===== Composer state (ввод/ответ) =====
        private bool _isComposerVisible;
        private bool _isReplyMode;
        private long _replyToMessageId;
        private string _replyPreviewText = "";
        private string _composerText = "";

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

        // ===== Composer bindings =====
        public bool IsComposerVisible
        {
            get => _isComposerVisible;
            set { _isComposerVisible = value; OnPropertyChanged(); }
        }

        public bool IsReplyMode
        {
            get => _isReplyMode;
            set
            {
                _isReplyMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReplyAutomationName));
                OnPropertyChanged(nameof(ComposerAutomationName));
            }
        }

        public long ReplyToMessageId
        {
            get => _replyToMessageId;
            set { _replyToMessageId = value; OnPropertyChanged(); }
        }

        public string ReplyPreviewText
        {
            get => _replyPreviewText;
            set
            {
                _replyPreviewText = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReplyAutomationName));
                OnPropertyChanged(nameof(ComposerAutomationName));
            }
        }

        // Было: "Ответ на сообщение: ..."
        // Теперь короче и понятнее:
        public string ReplyAutomationName => IsReplyMode
            ? ("Ответ на: " + (ReplyPreviewText ?? ""))
            : "";

        // ВАЖНО: это будет озвучиваться JAWS как имя поля ввода
        public string ComposerAutomationName => IsReplyMode
            ? ("Поле ввода. " + ReplyAutomationName)
            : "Поле ввода сообщения";

        public string ComposerText
        {
            get => _composerText;
            set { _composerText = value ?? ""; OnPropertyChanged(); }
        }

        public MessagesShellViewModel(TdLibService td, long chatId, string title)
        {
            _td = td;
            _chatId = chatId;
            _title = title;

            _ui = SynchronizationContext.Current ?? new SynchronizationContext();

            _td.UpdateReceivedPublic += OnUpdate;
        }

        public void StartCompose()
        {
            IsComposerVisible = true;
            IsReplyMode = false;
            ReplyToMessageId = 0;
            ReplyPreviewText = "";
        }

        public void StartReply(MessageDisplayItem? message)
        {
            if (message == null)
            {
                StartCompose();
                return;
            }

            IsComposerVisible = true;
            IsReplyMode = true;
            ReplyToMessageId = message.Id;

            ReplyPreviewText = (message.Text ?? "").Trim();
            if (ReplyPreviewText.Length > 180)
                ReplyPreviewText = ReplyPreviewText.Substring(0, 180) + "…";
        }

        public void CancelReply()
        {
            IsReplyMode = false;
            ReplyToMessageId = 0;
            ReplyPreviewText = "";
        }

        public void HideComposer()
        {
            IsComposerVisible = false;
            CancelReply();
        }

        public async Task<bool> SendCurrentAsync()
        {
            var text = (ComposerText ?? "").TrimEnd();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            await EnsureChatOpenedAsync();

            var send = new TdApi.SendMessage
            {
                ChatId = _chatId,
                InputMessageContent = new TdApi.InputMessageContent.InputMessageText
                {
                    Text = new TdApi.FormattedText { Text = text }
                }
            };

            if (IsReplyMode && ReplyToMessageId != 0)
                TrySetReplyOnSendMessage(send, ReplyToMessageId);

            try
            {
                await _td.ExecuteAsync(send);
            }
            catch
            {
                return false;
            }

            ComposerText = "";

            // После отправки ответа — режим ответа должен исчезнуть
            if (IsReplyMode)
                CancelReply();

            return true;
        }

        private static void TrySetReplyOnSendMessage(TdApi.SendMessage send, long replyToMessageId)
        {
            if (send == null || replyToMessageId == 0) return;

            var p1 = send.GetType().GetProperty("ReplyToMessageId", BindingFlags.Public | BindingFlags.Instance);
            if (p1 != null && p1.CanWrite && p1.PropertyType == typeof(long))
            {
                p1.SetValue(send, replyToMessageId);
                return;
            }

            var p2 = send.GetType().GetProperty("ReplyTo", BindingFlags.Public | BindingFlags.Instance);
            if (p2 != null && p2.CanWrite)
            {
                var replyObj = CreateInputMessageReplyToMessage(replyToMessageId);
                if (replyObj != null)
                {
                    p2.SetValue(send, replyObj);
                    return;
                }
            }

            var p3 = send.GetType().GetProperty("ReplyToMessage", BindingFlags.Public | BindingFlags.Instance);
            if (p3 != null && p3.CanWrite)
            {
                var replyObj = CreateInputMessageReplyToMessage(replyToMessageId);
                if (replyObj != null)
                {
                    p3.SetValue(send, replyObj);
                    return;
                }
            }
        }

        private static object? CreateInputMessageReplyToMessage(long replyToMessageId)
        {
            try
            {
                var asm = typeof(TdApi).Assembly;

                Type? t = null;

                foreach (var tt in asm.GetTypes())
                {
                    var n = tt.Name ?? "";
                    if (n.IndexOf("ReplyTo", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        n.IndexOf("Message", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (tt.IsClass && !tt.IsAbstract)
                        {
                            t = tt;
                            if (tt.GetProperty("MessageId") != null || tt.GetProperty("ReplyToMessageId") != null)
                                break;
                        }
                    }
                }

                if (t == null) return null;

                var obj = Activator.CreateInstance(t);
                if (obj == null) return null;

                var pMsgId = t.GetProperty("MessageId", BindingFlags.Public | BindingFlags.Instance);
                if (pMsgId != null && pMsgId.CanWrite && pMsgId.PropertyType == typeof(long))
                {
                    pMsgId.SetValue(obj, replyToMessageId);
                    return obj;
                }

                var pReplyId = t.GetProperty("ReplyToMessageId", BindingFlags.Public | BindingFlags.Instance);
                if (pReplyId != null && pReplyId.CanWrite && pReplyId.PropertyType == typeof(long))
                {
                    pReplyId.SetValue(obj, replyToMessageId);
                    return obj;
                }

                return null;
            }
            catch
            {
                return null;
            }
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

        private async Task<MessageDisplayItem> BuildDisplayItemAsync(TdApi.Message m)
        {
            string text = ExtractText(m);
            var dt = DateTimeOffset.FromUnixTimeSeconds(m.Date).ToLocalTime().DateTime;
            string author = await ResolveAuthor(m.SenderId);

            return new MessageDisplayItem(
                id: m.Id,
                text: text,
                meta: $"{author} {dt:dd.MM.yyyy HH:mm}"
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
            if (m.Content is TdApi.MessageContent.MessageText mt)
                return mt.Text?.Text ?? "(пусто)";

            if (m.Content is TdApi.MessageContent.MessagePhoto mp)
                return string.IsNullOrWhiteSpace(mp.Caption?.Text) ? "[Фото]" : "[Фото] " + mp.Caption.Text;

            if (m.Content is TdApi.MessageContent.MessageVideo mv)
                return string.IsNullOrWhiteSpace(mv.Caption?.Text) ? "[Видео]" : "[Видео] " + mv.Caption.Text;

            if (m.Content is TdApi.MessageContent.MessageDocument md)
                return string.IsNullOrWhiteSpace(md.Caption?.Text) ? "[Файл]" : "[Файл] " + md.Caption.Text;

            return "[Сообщение]";
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
                try { await _td.ExecuteAsync(new TdApi.CloseChat { ChatId = _chatId }); } catch { }
            });
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}