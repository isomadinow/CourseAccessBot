using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace CourseAccessBot.Extensions
{
    public static class TelegramBotClientExtensions
    {
        /// <summary>
        /// Отправляет сообщение с Inline-клавиатурой.
        /// </summary>
        [Obsolete]
        public static async Task SendMessageWithInlineKeyboardAsync(
            this ITelegramBotClient botClient,
            long chatId,
            string text,
            IEnumerable<InlineKeyboardButton[]> buttons)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(buttons);

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                replyMarkup: inlineKeyboard
            );
        }
    }
}
