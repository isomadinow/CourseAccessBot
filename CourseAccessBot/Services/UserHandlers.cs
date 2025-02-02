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
    private readonly PaymentRepository _paymentRepo;

    // Храним у каждого пользователя выбранный курс
    private static Dictionary<long, int> _userSelectedCourse = new();

    public UserHandlers(ITelegramBotClient botClient, CourseRepository courseRepo, PaymentRepository paymentRepo)
    {
        _botClient = botClient;
        _courseRepo = courseRepo;
        _paymentRepo = paymentRepo;
    }

    /// <summary>
    /// Отображает приветственное сообщение и меню для пользователя.
    /// </summary>
    public async Task ShowUserMenu(long chatId)
    {
        // Клавиатура для пользователя
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📚 Список курсов", "ℹ Контакты" },
            new KeyboardButton[] { "❓ Помощь" }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };

        // Приветствие с более «продающим» текстом
        string welcomeText =
            "👋 *Добро пожаловать в CourseBot!* \n\n" +
            "Мы рады предложить вам _лучшие_ и _самые актуальные_ курсы по различным направлениям. " +
            "Наша команда экспертов отобрала для вас только качественный материал, чтобы вы смогли " +
            "повысить свою квалификацию и добиться успеха!\n\n" +
            "Выберите действие из меню ниже, чтобы продолжить:";

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: welcomeText,
            parseMode: ParseMode.Markdown,  // Чтобы выделения *...* и _..._ корректно отображались
            replyMarkup: keyboard
        );
    }

    /// <summary>
    /// Обрабатывает входящие текстовые сообщения от пользователя.
    /// </summary>
    public async Task HandleUserTextMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;

        // Если пользователь прислал файл (чек), обрабатываем отдельно
        if (message.Type == MessageType.Photo ||
            (message.Type == MessageType.Document && message.Document!.MimeType == "application/pdf"))
        {
            await HandleUserFileMessage(message);
            return;
        }

        var text = message.Text;

        switch (text)
        {
            case "📚 Список курсов":
                await ShowCoursesList(chatId);
                break;
            case "ℹ Контакты":
                await ShowContacts(chatId);
                break;
            case "❓ Помощь":
                await ShowHelp(chatId);
                break;
            default:
                await _botClient.SendTextMessageAsync(chatId, "❌ Неизвестная команда. Пожалуйста, выберите действие из меню.");
                break;
        }
    }

    /// <summary>
    /// Показываем список всех доступных курсов.
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
                        text: $"{EscapeMarkdown(course.Title)} ({course.Price} руб.)",
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
            parseMode: ParseMode.Markdown, // Если хотим использовать Markdown
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );
    }

    /// <summary>
    /// Обрабатывает callback-кнопки от пользователя.
    /// </summary>
    public async Task HandleUserCallbackQuery(CallbackQuery callbackQuery)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var userId = callbackQuery.From.Id;
        var data = callbackQuery.Data;

        if (data == null) return;

        // Обработка кнопки "Вернуться в меню"
        if (data == "return_to_menu")
        {
            await ShowUserMenu(chatId);
            return;
        }

        // Обработка выбора конкретного курса
        if (data.StartsWith("select_course_"))
        {
            var courseIdStr = data.Replace("select_course_", "");
            if (int.TryParse(courseIdStr, out int courseId))
            {
                var course = _courseRepo.GetCourseById(courseId);
                if (course != null)
                {
                    // Сохраняем выбранный курс за пользователем
                    _userSelectedCourse[userId] = course.Id;

                    await _botClient.SendTextMessageAsync(chatId,
                        $"✅ Вы выбрали курс: *{EscapeMarkdown(course.Title)}*\n" +
                        $"💰 Цена: *{course.Price} руб.*\n\n" +
                        "📩 Пожалуйста, отправьте фото чека или PDF-файл, подтверждающий оплату, прямо в этот чат.",
                        parseMode: ParseMode.Markdown);

                    await ShowReturnToMenuButton(chatId);
                }
            }
        }
    }

    /// <summary>
    /// Обработка файлов (чеков), которые отправляет пользователь.
    /// </summary>
    public async Task HandleUserFileMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;

        // Проверяем, выбрал ли пользователь курс
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

        // Если файл не фотография или не PDF, сообщаем пользователю об ошибке
        if (fileId == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Неверный формат файла. Пришлите фото или PDF.");
            return;
        }

        // Создаём запись об оплате
        Guid paymentId = Guid.NewGuid();

        var payment = new PaymentInfo
        {
            Id = paymentId,
            UserId = userId,
            CourseId = courseId,
            Status = PaymentStatus.Pending,
            PhotoFileId = fileId
        };
        _paymentRepo.AddPayment(payment);

        // Формируем идентификатор пользователя для админов
        string userIdentifier = message.From.Username != null
            ? $"@{EscapeMarkdown(message.From.Username)}"
            : (!string.IsNullOrEmpty(message.From.FirstName) || !string.IsNullOrEmpty(message.From.LastName))
                ? $"{EscapeMarkdown(message.From.FirstName)} {EscapeMarkdown(message.From.LastName)}".Trim()
                : $"ID: {userId}";

        // Кнопки для администраторов
        var buttons = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Подтвердить", $"approve_{paymentId}"),
            InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"reject_{paymentId}")
        });

        // Отправляем файл и данные об оплате всем админам
        foreach (var adminId in AdminHandlers.GetAdmins())
        {
            // Сначала пересылаем сам чек (фото или PDF)
            await _botClient.ForwardMessageAsync(
                chatId: adminId,
                fromChatId: chatId,
                messageId: message.MessageId
            );

            // Затем отправляем текст с кнопками
            await _botClient.SendTextMessageAsync(
                chatId: adminId,
                text: $"💳 Новый чек от {userIdentifier} за курс *{EscapeMarkdown(course.Title)}*\n" +
                      $"💰 Цена: {course.Price} руб.",
                parseMode: ParseMode.Markdown,
                replyMarkup: buttons
            );
        }

        // Сообщаем пользователю, что чек принят на проверку
        await _botClient.SendTextMessageAsync(chatId, "✅ Ваш чек был успешно отправлен на проверку. Ожидайте решения администратора.");
    }

    /// <summary>
    /// Показывает кнопку "Вернуться в меню".
    /// </summary>
    private async Task ShowReturnToMenuButton(long chatId)
    {
        var returnButton = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Вернуться в меню", "return_to_menu")
        });

        await _botClient.SendTextMessageAsync(
            chatId,
            "Выберите следующее действие:",
            replyMarkup: returnButton
        );
    }

    /// <summary>
    /// Показываем контакты (телефон, e-mail).
    /// </summary>
    private async Task ShowContacts(long chatId)
    {
        await _botClient.SendTextMessageAsync(chatId,
            "📞 *Контакты*:\n" +
            "- Email: support@coursebot.com\n" +
            "- Телефон: +123456789\n\n" +
            "Если возникнут вопросы, напишите администратору: @isomadinow",
            parseMode: ParseMode.Markdown
        );
    }

    /// <summary>
    /// Показываем помощь/FAQ.
    /// </summary>
    private async Task ShowHelp(long chatId)
    {
        await _botClient.SendTextMessageAsync(chatId,
            "ℹ *Помощь*\n\n" +
            "1. Нажмите \"📚 Список курсов\" и выберите интересующий вас курс.\n" +
            "2. Отправьте чек об оплате (фото или PDF), чтобы мы могли подтвердить вашу покупку.\n" +
            "3. Дождитесь ответа от администратора — после подтверждения вы получите доступ к курсу.\n\n" +
            "Если у вас остались вопросы, обратитесь к администратору: @isomadinow",
            parseMode: ParseMode.Markdown
        );
    }

    /// <summary>
    /// Утилита для экранирования символов Markdown.
    /// </summary>
    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("~", "\\~")
            .Replace("`", "\\`")
            .Replace(">", "\\>")
            .Replace("#", "\\#")
            .Replace("+", "\\+")
            .Replace("-", "\\-")
            .Replace("=", "\\=")
            .Replace("|", "\\|")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace(".", "\\.")
            .Replace("!", "\\!");
    }
}
