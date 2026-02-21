using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TelegramWin.Services;

namespace TelegramWin.ViewModels
{
    /// <summary>
    /// Временная VM для окна сообщений.
    /// Сейчас хранит тестовые сообщения; позже заменим на реальные сообщения из TDLib.
    /// </summary>
    public sealed class MessagesShellViewModel : INotifyPropertyChanged
    {
        private readonly TdLibService _td;
        private readonly long _chatId;

        private string _title;

        // Важно для доступности: при открытии окна JAWS не должен озвучивать "Чат ID ...",
        // поэтому по умолчанию и после загрузки — пусто.
        private string _status = "";

        private MessageDisplayItem? _selectedMessage;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<MessageDisplayItem> Messages { get; } = new();

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Строка статуса (отладка/индикаторы). Сейчас держим пустой, чтобы не мешать JAWS.
        /// При необходимости можно показывать chatId в Title или отдельной кнопкой "Инфо".
        /// </summary>
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

        public MessagesShellViewModel(TdLibService td, long chatId, string title)
        {
            _td = td;
            _chatId = chatId;
            _title = title;
        }

        public void LoadTestMessages()
        {
            Messages.Clear();

            // Порядок как в Telegram: старые сверху, новые снизу.
            for (int i = 1; i <= 30; i++)
            {
                var text = $"Тестовое сообщение {i}";
                var meta = $"Автор {System.DateTime.Now:dd.MM.yyyy HH:mm}";
                Messages.Add(new MessageDisplayItem(text, meta));
            }

            // Не озвучиваем chatId через статус (JAWS это читает при открытии окна).
            Status = "";
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
