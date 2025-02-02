using CourseAccessBot.Models;
using CourseAccessBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CourseAccessBot.Services;

public class UserHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly CourseRepository _courseRepo;

    // userId -> выбранный courseId
    private static Dictionary<long, int> _userSelectedCourse = new();

    public UserHandlers(ITelegramBotClient botClient, CourseRepository courseRepo)
    {
        _botClient = botClient;
        _courseRepo = courseRepo;
    }

    /// <summary>
    /// Показывает главное меню пользователя (всегда внизу).
    /// </summary>
    public async Task ShowUserMenu(long chatId)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📚 Список курсов" }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "📋 Главное меню пользователя:\nВыберите действие:",
            replyMarkup: keyboard
        );
    }

    /// <summary>
    /// Обрабатывает нажатия на текстовые команды из Reply-клавиатуры.
    /// </summary>
    public async Task HandleUserTextMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var text = message.Text;

        if (text == "📚 Список курсов")
        {
            await ShowCoursesList(chatId);
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Неизвестная команда. Выберите действие из меню.");
        }
    }

    /// <summary>
    /// Показывает список курсов с Inline-кнопками.
    /// </summary>
    private async Task ShowCoursesList(long chatId)
    {
        var courses = _courseRepo.GetAllCourses().ToList();
        List<InlineKeyboardButton[]> buttons = new();

        if (courses.Any())
        {
            foreach (var course in courses)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        text: $"{course.Title} ({course.Price} руб.)",
                        callbackData: $"select_course_{course.Id}")
                });
            }
        }
        else
        {
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Курсы пока не добавлены", "no_action") });
        }

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Выберите курс:",
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );
    }

    /// <summary>
    /// Обрабатывает нажатия на Inline-кнопки (выбор курса).
    /// </summary>
    public async Task HandleUserCallbackQuery(CallbackQuery callbackQuery)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var userId = callbackQuery.From.Id;
        var data = callbackQuery.Data;

        if (data == null) return;

        if (data.StartsWith("select_course_"))
        {
            var courseIdStr = data.Replace("select_course_", "");
            if (int.TryParse(courseIdStr, out int courseId))
            {
                var course = _courseRepo.GetCourseById(courseId);
                if (course != null)
                {
                    _userSelectedCourse[userId] = course.Id;

                    await _botClient.SendTextMessageAsync(chatId,
                        $"✅ Вы выбрали курс: *{course.Title}*\n" +
                        $"💰 Цена: *{course.Price} руб.*\n\n" +
                        "📩 Для оплаты отправьте чек (фото или PDF) в этот чат.",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

                    // Добавляем кнопку "🔙 Вернуться в меню"
                    await ShowReturnToMenuButton(chatId);
                }
            }
        }
    }

    /// <summary>
    /// Кнопка возврата в главное меню.
    /// </summary>
    private async Task ShowReturnToMenuButton(long chatId)
    {
        var returnButton = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Вернуться в меню", "return_to_menu")
        });

        await _botClient.SendTextMessageAsync(chatId, "Выберите следующее действие:", replyMarkup: returnButton);
    }
}
