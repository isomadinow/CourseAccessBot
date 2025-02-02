using CourseAccessBot.Models;
using CourseAccessBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CourseAccessBot.Services;

public class AdminHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly CourseRepository _courseRepo;
    private readonly PaymentRepository _paymentRepo;


    private static Dictionary<long, string> _adminStates = new();
    private static Dictionary<long, Course> _newCourseData = new();

    public AdminHandlers(ITelegramBotClient botClient, CourseRepository courseRepo, PaymentRepository paymentRepo)
    {
        _botClient = botClient;
        _courseRepo = courseRepo;
        _paymentRepo = paymentRepo;
    }

    public bool IsAdminInAddingCourseState(long userId)
    {
        return _adminStates.ContainsKey(userId);
    }

    public async Task ShowAdminMenu(long chatId)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "➕ Добавить курс", "❌ Удалить курс" },
            new KeyboardButton[] { "🔍 Проверить оплаты", "📋 Все курсы" }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };

        await _botClient.SendTextMessageAsync(chatId, "📋 Главное меню администратора:", replyMarkup: keyboard);
    }

    public async Task HandleAdminTextMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var text = message.Text;

        if (_adminStates.ContainsKey(userId))
        {
            await HandleCourseAddingProcess(userId, chatId, text);
            return;
        }

        switch (text)
        {
            case "➕ Добавить курс":
                await StartCourseAdding(userId, chatId);
                break;

            case "❌ Удалить курс":
                await ShowCoursesForDeletion(chatId);
                break;

            case "🔍 Проверить оплаты":
                await ShowPendingPayments(chatId);
                break;
            case "📋 Все курсы": // Добавлено
                await ShowAllCourse(chatId);
                break;
            default:
                await _botClient.SendTextMessageAsync(chatId, "❌ Неизвестная команда. Выберите действие из меню.");
                break;
        }
    }


 
    
    private async Task StartCourseAdding(long userId, long chatId)
    {
        _adminStates[userId] = "awaiting_title";
        _newCourseData[userId] = new Course();
        await _botClient.SendTextMessageAsync(chatId, "📚 Введите название курса:");
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

                await _botClient.SendTextMessageAsync(chatId,
                    $"✅ Курс \"{courseData.Title}\" добавлен успешно!\n\n" +
                    $"📋 Название: {courseData.Title}\n" +
                    $"📝 Описание: {courseData.Description}\n" +
                    $"💰 Цена: {courseData.Price} руб.\n" +
                    $"🔗 Ссылка сохранена:{courseData.Link}");

                await ShowAdminMenu(chatId);
                break;
        }
    }

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

    private async Task DeleteCourse(long chatId, int courseId)
    {
        var course = _courseRepo.GetCourseById(courseId);
        if (course == null)
        {
            await _botClient.SendTextMessageAsync(chatId, $"❌ Курс с ID {courseId} не найден.");
            return;
        }

        var result = _courseRepo.RemoveCourse(courseId);
        if (result)
        {
            await _botClient.SendTextMessageAsync(chatId, $"✅ Курс \"{course.Title}\" успешно удалён.");
            await ShowCoursesForDeletion(chatId); // Обновляем список курсов
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка при удалении курса с ID {courseId}.");
        }
    }

    private async Task ShowCoursesForDeletion(long chatId)
    {
        var courses = _courseRepo.GetAllCourses().ToList();
        if (!courses.Any())
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Нет доступных курсов для удаления.");
            return;
        }

        foreach (var course in courses)
        {
            var buttons = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Удалить", $"delete_course_{course.Id}")
            });

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"📚 Курс: {course.Title}\nID: {course.Id}\nЦена: {course.Price} руб.",
                replyMarkup: buttons
            );
        }
    }

    private async Task ShowPendingPayments(long chatId)
    {
        var pendingPayments = _paymentRepo.GetPendingPayments().ToList();
        if (!pendingPayments.Any())
        {
            await _botClient.SendTextMessageAsync(chatId, "🔍 Нет оплат, ожидающих проверки.");
            return;
        }

        foreach (var payment in pendingPayments)
        {
            var course = _courseRepo.GetCourseById(payment.CourseId);
            if (course == null) continue;

            var buttons = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Принять", $"approve_{payment.Id}"),
                InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"reject_{payment.Id}")
            });

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"💳 Оплата от пользователя {payment.UserId}:\n" +
                      $"📚 Курс: {course.Title}\n💰 Цена: {course.Price} руб.",
                replyMarkup: buttons
            );
        }
    }
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
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"📚 Название: {course.Title}\n" +
                      $"📝 Описание: {course.Description}\n" +
                      $"💰 Цена: {course.Price} руб.\n" +
                      $"🆔 ID: {course.Id}\n" +
                      $"🔗 Ссылка: {course.Link}"
            );
        }

        // После отображения всех курсов возвращаем в главное меню
        await _botClient.SendTextMessageAsync(
            chatId,
            "📋 Все курсы показаны. Возвращаю в главное меню...",
            replyMarkup: new ReplyKeyboardRemove() // Удаляем текущую клавиатуру, если нужно
        );

        await ShowAdminMenu(chatId); // Возврат в главное меню
    }

    private async Task HandlePaymentApproval(CallbackQuery callbackQuery, string data)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var paymentIdStr = data.Replace("approve_", "").Replace("reject_", "");

        if (Guid.TryParse(paymentIdStr, out Guid paymentId))
        {
            var payment = _paymentRepo.GetPaymentById(paymentId);
            if (payment == null)
            {
                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "❌ Оплата не найдена.");
                return;
            }

            if (data.StartsWith("approve_"))
            {
                payment.Status = PaymentStatus.Approved;
                _paymentRepo.UpdatePayment(payment);

                var course = _courseRepo.GetCourseById(payment.CourseId);
                if (course == null)
                {
                    await _botClient.SendTextMessageAsync(payment.UserId, "❌ Курс не найден. Свяжитесь с поддержкой.");
                    return;
                }

                await _botClient.SendTextMessageAsync(
                    payment.UserId,
                    $"✅ Ваша оплата подтверждена!\n" +
                    $"🎓 Вы получили доступ к курсу \"{course.Title}\".\n" +
                    $"🔗 Ваша ссылка: {course.Link}"
                );

                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "✅ Оплата подтверждена. Ссылка отправлена пользователю.");
            }
            else
            {
                payment.Status = PaymentStatus.Rejected;
                _paymentRepo.UpdatePayment(payment);

                await _botClient.SendTextMessageAsync(
                    payment.UserId,
                    "❌ Ваша оплата была отклонена. Обратитесь в поддержку."
                );

                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "❌ Оплата отклонена.");
            }
        }
    }
}
