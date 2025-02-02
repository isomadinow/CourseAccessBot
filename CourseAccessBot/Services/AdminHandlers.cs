using CourseAccessBot.Models;
using CourseAccessBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CourseAccessBot.Services;

public class AdminHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly CourseRepository _courseRepo;
    private readonly PaymentRepository _paymentRepo;

    // Список всех админов
    private static List<long> _adminIds = new();

    // Состояния администратора при добавлении курса
    private static Dictionary<long, string> _adminStates = new();
    private static Dictionary<long, Course> _newCourseData = new();

    public AdminHandlers(ITelegramBotClient botClient,
                         CourseRepository courseRepo,
                         PaymentRepository paymentRepo,
                         List<long> adminIds)
    {
        _botClient = botClient;
        _courseRepo = courseRepo;
        _paymentRepo = paymentRepo;
        _adminIds = adminIds;
    }

    /// <summary>
    /// Возвращает список ID администраторов.
    /// </summary>
    public static List<long> GetAdmins()
    {
        return _adminIds;
    }

    /// <summary>
    /// Проверяет, находится ли админ в процессе добавления курса.
    /// </summary>
    public bool IsAdminInAddingCourseState(long userId)
    {
        return _adminStates.ContainsKey(userId);
    }

    /// <summary>
    /// Показывает главное меню администратора с приветствием.
    /// </summary>
    public async Task ShowAdminMenu(long chatId)
    {
        // Клавиатура меню администратора
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "➕ Добавить курс", "❌ Удалить курс" },
            new KeyboardButton[] {  "📋 Все курсы" }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };

        // Можно сделать «продающий» или информативный текст для админа
        var adminMenuText =
            "🔑 *Административная панель*\n\n" +
            "Добро пожаловать в панель управления! Здесь вы можете:\n" +
            "• Добавлять и удалять курсы\n" +
            "• Проверять оплаты от пользователей\n" +
            "• Просматривать весь список курсов\n\n" +
            "Выберите действие из меню ниже:";

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: adminMenuText,
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard
        );
    }

    /// <summary>
    /// Обработка команд от администратора (текстовые сообщения).
    /// </summary>
    public async Task HandleAdminTextMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var text = message.Text;

        // Если админ в процессе добавления курса, продолжаем диалог добавления
        if (_adminStates.ContainsKey(userId))
        {
            await HandleCourseAddingProcess(userId, chatId, text);
            return;
        }

        // Обработка текстовых команд администратора
        switch (text)
        {
            case "➕ Добавить курс":
                await StartCourseAdding(userId, chatId);
                break;

            case "❌ Удалить курс":
                await ShowCoursesForDeletion(chatId);
                break;

            case "📋 Все курсы":
                await ShowAllCourse(chatId);
                break;

            default:
                await _botClient.SendTextMessageAsync(
                    chatId,
                    "❌ Неизвестная команда. Пожалуйста, выберите действие из меню."
                );
                break;
        }
    }

    #region Методы добавления нового курса

    /// <summary>
    /// Начинаем процесс добавления курса (запрашиваем у админа название).
    /// </summary>
    private async Task StartCourseAdding(long userId, long chatId)
    {
        _adminStates[userId] = "awaiting_title";
        _newCourseData[userId] = new Course();

        await _botClient.SendTextMessageAsync(
            chatId,
            "📚 Введите название курса:"
        );
    }

    private async Task HandleCourseAddingProcess(long userId, long chatId, string text)
    {
        if (!_adminStates.ContainsKey(userId)) return;

        var currentState = _adminStates[userId];
        var courseData = _newCourseData[userId];

        switch (currentState)
        {
            case "awaiting_title":
                courseData.Title = text;
                _adminStates[userId] = "awaiting_description";
                await _botClient.SendTextMessageAsync(chatId, "📝 Введите описание курса:");
                break;

            case "awaiting_description":
                courseData.Description = text;
                _adminStates[userId] = "awaiting_price";
                await _botClient.SendTextMessageAsync(chatId, "💰 Введите цену курса (число в рублях):");
                break;

            case "awaiting_price":
                if (!decimal.TryParse(text, out decimal price))
                {
                    await _botClient.SendTextMessageAsync(chatId, "❌ Некорректный формат цены. Введите число.");
                    return;
                }
                courseData.Price = price;
                _adminStates[userId] = "awaiting_link";
                await _botClient.SendTextMessageAsync(chatId, "🔗 Введите ссылку на курс:");
                break;

            case "awaiting_link":
                courseData.Link = text;
                _courseRepo.AddCourse(courseData);

                _adminStates.Remove(userId);
                _newCourseData.Remove(userId);

                try
                {
                    string messageText = $"✅ Курс успешно добавлен!\n\n" +
                                         $"📚 *Название:* {EscapeMarkdown(courseData.Title)}\n" +
                                         $"📝 *Описание:* {EscapeMarkdown(courseData.Description)}\n" +
                                         $"💰 *Цена:* {courseData.Price} руб.\n" +
                                         $"🔗 *Ссылка:* {EscapeMarkdown(courseData.Link)}";

                    await _botClient.SendTextMessageAsync(
                        chatId,
                        messageText,
                        parseMode: ParseMode.Markdown
                    );

                    // ⏪ Возвращаем в главное меню
                    await ShowAdminMenu(chatId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ Ошибка отправки сообщения: {ex.Message}");
                    await _botClient.SendTextMessageAsync(chatId, "✅ Курс успешно добавлен, но возникла ошибка при отображении данных.");

                    // Все равно возвращаем в главное меню!
                    await ShowAdminMenu(chatId);
                }
                break;
        }
    }

    #endregion

    /// <summary>
    /// Обработка колбэк-запросов (inline-кнопки): удаление курса / подтверждение или отклонение оплаты.
    /// </summary>
    public async Task HandleAdminCallbackQuery(CallbackQuery callbackQuery)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data;

        if (data.StartsWith("delete_course_"))
        {
            var courseIdStr = data.Replace("delete_course_", "");
            if (int.TryParse(courseIdStr, out int courseId))
            {
                await DeleteCourse(chatId, courseId);
                return;
            }
        }
        else if (data.StartsWith("approve_") || data.StartsWith("reject_"))
        {
            await HandlePaymentApproval(callbackQuery, data);
        }
    }

    #region Удаление курсов

    /// <summary>
    /// Показываем список курсов для удаления.
    /// </summary>
    private async Task ShowCoursesForDeletion(long chatId)
    {
        var courses = _courseRepo.GetAllCourses().ToList();
        if (!courses.Any())
        {
            await _botClient.SendTextMessageAsync(
                chatId,
                "❌ Нет доступных курсов для удаления."
            );
            return;
        }

        foreach (var course in courses)
        {
            var buttons = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "❌ Удалить",
                    $"delete_course_{course.Id}"
                )
            });

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text:
                    $"*Курс:* {course.Title}\n" +
                    $"*ID:* {course.Id}\n" +
                    $"*Цена:* {course.Price} руб.",
                parseMode: ParseMode.Markdown,
                replyMarkup: buttons
            );
        }
    }

    /// <summary>
    /// Удаляем курс по его ID.
    /// </summary>
    private async Task DeleteCourse(long chatId, int courseId)
    {
        var course = _courseRepo.GetCourseById(courseId);
        if (course == null)
        {
            await _botClient.SendTextMessageAsync(
                chatId,
                $"❌ Курс с ID {courseId} не найден."
            );
            return;
        }

        var result = _courseRepo.RemoveCourse(courseId);
        if (result)
        {
            await _botClient.SendTextMessageAsync(
                chatId,
                $"✅ Курс \"{course.Title}\" успешно удалён."
            );

            // Показываем список оставшихся курсов (если администратору нужно удалить ещё что-то)
            await ShowCoursesForDeletion(chatId);
        }
        else
        {
            await _botClient.SendTextMessageAsync(
                chatId,
                $"❌ Ошибка при удалении курса с ID {courseId}."
            );
        }
    }

    #endregion

   

    /// <summary>
    /// Обработка подтверждения или отклонения оплаты.
    /// </summary>
    private async Task HandlePaymentApproval(CallbackQuery callbackQuery, string data)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;
        var paymentIdStr = data.Replace("approve_", "").Replace("reject_", "");


        if (Guid.TryParse(paymentIdStr, out Guid paymentId))
        {
            var payment = _paymentRepo.GetPaymentById(paymentId);
            if (payment == null)
            {
                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "❌ Оплата не найдена.");
                return;
            }
   
            // Если платеж подтверждается
            if (data.StartsWith("approve_"))
            {
                payment.Status = PaymentStatus.Approved;
                _paymentRepo.UpdatePayment(payment);

                var course = _courseRepo.GetCourseById(payment.CourseId);
                if (course == null)
                {
                    try
                    {
                        await _botClient.SendTextMessageAsync(
                            payment.UserId,
                            "❌ Курс не найден. Обратитесь к администратору."
                        );
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                    {
                        if (ex.Message.Contains("bot was blocked by the user"))
                        {
                            Console.WriteLine($"Пользователь {payment.UserId} заблокировал бота.");
                        }
                        else
                        {
                            throw;
                        }
                    }
                    return;
                }

                // Отправляем подтверждение пользователю
                try
                {
                    await _botClient.SendTextMessageAsync(
                        payment.UserId,
                        $"✅ Ваша оплата подтверждена!\n" +
                        $"🎓 Вы получили доступ к курсу \"{course.Title}\".\n" +
                        $"🔗 Ссылка: {course.Link}"
                    );
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                {
                    if (ex.Message.Contains("bot was blocked by the user"))
                    {
                        Console.WriteLine($"Пользователь {payment.UserId} заблокировал бота.");
                    }
                    else
                    {
                        throw;
                    }
                }

                // Обновляем сообщение у администратора
                var updatedText =
                    $"✅ Вы подтвердили оплату от {payment.UserId} за курс \"{course.Title}\".\n" +
                    $"💰 Сумма: {course.Price} руб.";

                if (!string.Equals(callbackQuery.Message.Text, updatedText, StringComparison.OrdinalIgnoreCase))
                {
                    await _botClient.EditMessageTextAsync(
                        chatId: chatId,
                        messageId: messageId,
                        text: updatedText,
                        parseMode: ParseMode.Markdown
                    );
                }
            }
            else // Отклонение оплаты
            {
                payment.Status = PaymentStatus.Rejected;
                _paymentRepo.UpdatePayment(payment);

                // Уведомляем пользователя, что оплата отклонена
                try
                {
                    await _botClient.SendTextMessageAsync(
                        payment.UserId,
                        "❌ Ваша оплата была отклонена. " +
                        "Пожалуйста, обратитесь к администратору для уточнения деталей."
                    );
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                {
                    if (ex.Message.Contains("bot was blocked by the user"))
                    {
                        Console.WriteLine($"Пользователь {payment.UserId} заблокировал бота.");
                    }
                    else
                    {
                        throw;
                    }
                }

                // Обновляем сообщение у администратора
                var updatedText =
                    $"❌ Вы отклонили оплату от {payment.UserId} " +
                    $"за курс \"{payment.CourseId}\".";

                if (!string.Equals(callbackQuery.Message.Text, updatedText, StringComparison.OrdinalIgnoreCase))
                {
                    await _botClient.EditMessageTextAsync(
                        chatId: chatId,
                        messageId: messageId,
                        text: updatedText,
                        parseMode: ParseMode.Markdown
                    );
                }
            }

            // Убираем кнопки из сообщения
            try
            {
                await _botClient.EditMessageReplyMarkupAsync(
                    chatId: chatId,
                    messageId: messageId,
                    replyMarkup: null
                );
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                // Если получаем "message is not modified", значит кнопок уже нет
                if (!ex.Message.Contains("message is not modified"))
                    throw;
            }

            // Сообщаем боту, что действие завершено
            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "✅ Обработка завершена.");
        }
    }



    /// <summary>
    /// Показать все курсы (админу).
    /// </summary>
    private async Task ShowAllCourse(long chatId)
    {
        var courses = _courseRepo.GetAllCourses().ToList();

        if (!courses.Any())
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Нет доступных курсов.");
            await ShowAdminMenu(chatId); // Возврат в меню, если нет курсов
            return;
        }

        foreach (var course in courses)
        {
            string messageText = $"📚 *Название:* {EscapeMarkdown(course.Title)}\n" +
                                 $"📝 *Описание:* {EscapeMarkdown(course.Description)}\n" +
                                 $"💰 *Цена:* {course.Price} руб.\n" +
                                 $"🆔 *ID:* {course.Id}\n" +
                                 $"🔗 *Ссылка:* {EscapeMarkdown(course.Link)}";

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: messageText,
                parseMode: ParseMode.Markdown
            );
        }

        await _botClient.SendTextMessageAsync(
            chatId,
            "📋 Все курсы показаны. Возвращаю в главное меню...",
            replyMarkup: new ReplyKeyboardRemove()
        );

        await ShowAdminMenu(chatId);
    }

    // Метод для экранирования символов в Markdown-разметке Telegram
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
