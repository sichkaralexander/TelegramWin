namespace TelegramWin.ViewModels
{
    public sealed class MessageDisplayItem
    {
        public string Text { get; }
        public string Meta { get; }

        // Для JAWS: читаемая строка в одну линию.
        public string AccessibleText => $"{Text}. {Meta}";

        public MessageDisplayItem(string text, string meta)
        {
            Text = text ?? "";
            Meta = meta ?? "";
        }
    }
}
