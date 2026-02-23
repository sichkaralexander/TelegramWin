using System;
using TdLib;

namespace TelegramWin.ViewModels
{
    /// <summary>
    /// Единый форматтер превью/текста сообщения для UI.
    /// Нужен, чтобы строка "последнее сообщение" была максимально похожа на то,
    /// что видит пользователь в окне сообщений (особенно для медиа/служебных сообщений).
    /// </summary>
    public static class MessagePreviewFormatter
    {
        public static string FromMessage(TdApi.Message? msg)
        {
            if (msg == null) return "";

            try
            {
                return FromContent(msg.Content);
            }
            catch
            {
                return "";
            }
        }

        public static string FromContent(TdApi.MessageContent? content)
        {
            if (content == null) return "";

            // Текст
            if (content is TdApi.MessageContent.MessageText mt)
                return mt.Text?.Text ?? "";

            // Подпись у медиа
            static string Cap(TdApi.FormattedText? t) => t?.Text ?? "";

            // Фото
            if (content is TdApi.MessageContent.MessagePhoto mp)
            {
                var c = Cap(mp.Caption);
                return string.IsNullOrWhiteSpace(c) ? "[Фото]" : c;
            }

            // Видео
            if (content is TdApi.MessageContent.MessageVideo mv)
            {
                var c = Cap(mv.Caption);
                return string.IsNullOrWhiteSpace(c) ? "[Видео]" : c;
            }

            // Документ / файл
            if (content is TdApi.MessageContent.MessageDocument md)
            {
                var c = Cap(md.Caption);
                if (!string.IsNullOrWhiteSpace(c)) return c;
                var fileName = md.Document?.FileName ?? "";
                return string.IsNullOrWhiteSpace(fileName) ? "[Документ]" : $"[Документ] {fileName}";
            }

            // Анимация (GIF)
            if (content is TdApi.MessageContent.MessageAnimation ma)
            {
                var c = Cap(ma.Caption);
                return string.IsNullOrWhiteSpace(c) ? "[GIF]" : c;
            }

            // Голосовое
            if (content is TdApi.MessageContent.MessageVoiceNote vn)
            {
                var c = Cap(vn.Caption);
                return string.IsNullOrWhiteSpace(c) ? "[Голосовое сообщение]" : c;
            }

            // Аудио
            if (content is TdApi.MessageContent.MessageAudio aud)
            {
                var c = Cap(aud.Caption);
                if (!string.IsNullOrWhiteSpace(c)) return c;
                var title = aud.Audio?.Title ?? "";
                var performer = aud.Audio?.Performer ?? "";
                var s = (performer + " " + title).Trim();
                return string.IsNullOrWhiteSpace(s) ? "[Аудио]" : $"[Аудио] {s}";
            }

            // Стикер
            if (content is TdApi.MessageContent.MessageSticker)
                return "[Стикер]";

            // Гео
            if (content is TdApi.MessageContent.MessageLocation)
                return "[Геолокация]";

            // Контакт
            if (content is TdApi.MessageContent.MessageContact mc)
            {
                var name = ((mc.Contact?.FirstName ?? "") + " " + (mc.Contact?.LastName ?? "")).Trim();
                return string.IsNullOrWhiteSpace(name) ? "[Контакт]" : $"[Контакт] {name}";
            }

            // Опрос
            if (content is TdApi.MessageContent.MessagePoll poll)
            {
                var q = poll.Poll?.Question?.Text ?? "";
                return string.IsNullOrWhiteSpace(q) ? "[Опрос]" : $"[Опрос] {q}";
            }

            // Переслано/служебное/прочее — даём нейтральные подписи, чтобы не было пусто
            var typeName = content.GetType().Name;
            return typeName switch
            {
                "MessageChatAddMembers" => "[Добавлены участники]",
                "MessageChatDeleteMember" => "[Участник удалён]",
                "MessageChatChangeTitle" => "[Изменено название чата]",
                "MessageChatChangePhoto" => "[Изменено фото чата]",
                "MessagePinMessage" => "[Закреплено сообщение]",
                "MessageCall" => "[Звонок]",
                _ => ""
            };
        }
    }
}
