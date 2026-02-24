namespace TelegramWin.ViewModels
{
    public sealed class MessageDisplayItem
    {
        public long Id { get; }
        public string Text { get; }
        public string Meta { get; }

        // Для JAWS: читаемая строка в одну линию.
        public string AccessibleText => $"{Text}. {Meta}";

        public MessageDisplayItem(string text, string meta)
            : this(0, text, meta)
        {
        }

        public MessageDisplayItem(long id, string text, string meta)
        {
            Id = id;
            Text = text ?? "";
            Meta = meta ?? "";
        }
    }
}