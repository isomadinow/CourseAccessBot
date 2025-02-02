using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CourseAccessBot.Services;

public class BotHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly UserHandlers _userHandlers;
    private readonly AdminHandlers _adminHandlers;
    private readonly List<long> _adminIds;

    public BotHandlers(
        ITelegramBotClient botClient,
        UserHandlers userHandlers,
        AdminHandlers adminHandlers,
        List<long> adminIds)
    {
        _botClient = botClient;
        _userHandlers = userHandlers;
        _adminHandlers = adminHandlers;
        _adminIds = adminIds;
    }

    /// <summary>
    /// Основная точка обработки обновлений Telegram.
    /// </summary>
    public async Task HandleUpdateAsync(Update update)
    {
        if (update.Type == UpdateType.Message && update.Message is not null)
        {
            await HandleMessageAsync(update.Message);
        }
        else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is not null)
        {
            await HandleCallbackQueryAsync(update.CallbackQuery);
        }
    }

    /// <summary>
    /// Обработка текстовых сообщений от пользователей и администраторов.
    /// </summary>
    private async Task HandleMessageAsync(Message message)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var text = message.Text;

        if (IsAdmin(userId) && _adminHandlers.IsAdminInAddingCourseState(userId))
        {
            await _adminHandlers.HandleAdminTextMessage(message);
            return;
        }

        if (text == "/start")
        {
            if (IsAdmin(userId))
            {
                await _adminHandlers.ShowAdminMenu(chatId); // Админ-меню
            }
            else
            {
                await _userHandlers.ShowUserMenu(chatId); // Пользовательское меню
            }
        }
        else
        {
            if (IsAdmin(userId))
            {
                await _adminHandlers.HandleAdminTextMessage(message);
            }
            else
            {
                await _userHandlers.HandleUserTextMessage(message);
            }
        }
    }

    /// <summary>
    /// Обработка CallbackQuery.
    /// </summary>
    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
    {
        var userId = callbackQuery.From.Id;

        if (IsAdmin(userId))
        {
            await _adminHandlers.HandleAdminCallbackQuery(callbackQuery);
        }
        else
        {
            await _userHandlers.HandleUserCallbackQuery(callbackQuery);
        }
    }

    /// <summary>
    /// Проверка, является ли пользователь администратором.
    /// </summary>
    private bool IsAdmin(long userId)
    {
        return _adminIds.Contains(userId);
    }
}
