using CourseAccessBot.Models;
using CourseAccessBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
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
                        parseMode: ParseMode.Markdown);

                    // Добавляем кнопку "🔙 Вернуться в меню"
                    await ShowReturnToMenuButton(chatId);
                }
            }
        }
    }

    /// <summary>
    /// Обработка отправленных файлов (фото и PDF).
    /// </summary>
    public async Task HandleUserFileMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;

        if (!_userSelectedCourse.ContainsKey(userId))
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Вы не выбрали курс. Сначала выберите курс из списка.");
            return;
        }

        var courseId = _userSelectedCourse[userId];
        var course = _courseRepo.GetCourseById(courseId);

        if (course == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Курс не найден. Попробуйте выбрать курс заново.");
            return;
        }

        string? fileId = null;

        if (message.Type == MessageType.Photo)
        {
            var photo = message.Photo?.LastOrDefault();
            if (photo != null)
            {
                fileId = photo.FileId;
            }
        }
        else if (message.Type == MessageType.Document && message.Document!.MimeType == "application/pdf")
        {
            fileId = message.Document.FileId;
        }

        if (fileId == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Неверный формат файла. Пришлите фото или PDF.");
            return;
        }

        // Определяем, что отправлять: username, имя/фамилию или ID
        string userIdentifier = message.From.Username != null
            ? $"@{message.From.Username}"
            : (!string.IsNullOrEmpty(message.From.FirstName) || !string.IsNullOrEmpty(message.From.LastName))
                ? $"{message.From.FirstName} {message.From.LastName}".Trim()
                : $"ID: {userId}";

        // Отправляем файл админам
        foreach (var adminId in AdminHandlers.GetAdminIds())
        {
            await _botClient.SendDocumentAsync(
                chatId: adminId,
                document: new InputFileId(fileId),
                caption: $"💳 Новый чек от {userIdentifier} за курс \"{course.Title}\"."
            );
        }

        await _botClient.SendTextMessageAsync(chatId, "✅ Ваш файл был успешно отправлен на проверку.");
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
