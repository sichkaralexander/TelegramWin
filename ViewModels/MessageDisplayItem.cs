namespace TelegramWin.ViewModels
{
    public sealed class MessageDisplayItem
    {
        public long Id { get; }
        public string Text { get; }
        public string Meta { get; }
        public bool IsOutgoing { get; }

        // Для JAWS: читаемая строка в одну линию.
        public string AccessibleText => $"{Text}. {Meta}";

        public MessageDisplayItem(string text, string meta)
            : this(0, text, meta, false)
        {
        }

        public MessageDisplayItem(long id, string text, string meta)
            : this(id, text, meta, false)
        {
        }

        public MessageDisplayItem(long id, string text, string meta, bool isOutgoing)
        {
            Id = id;
            Text = text ?? "";
            Meta = meta ?? "";
            IsOutgoing = isOutgoing;
        }
    }
}
