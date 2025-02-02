using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace CourseAccessBot.Services;

public class BotService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotHandlers _botHandlers;

    public BotService(ITelegramBotClient botClient, BotHandlers botHandlers)
    {
        _botClient = botClient;
        _botHandlers = botHandlers;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Получаем все типы обновлений
        };

        _botClient.StartReceiving(
            async (botClient, update, ct) =>
            {
                try
                {
                    await _botHandlers.HandleUpdateAsync(update);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обработки обновления: {ex.Message}");
                }
            },
            async (botClient, exception, ct) =>
            {
                Console.WriteLine($"Ошибка в поллинге: {exception.Message}");
                await Task.CompletedTask;
            },
            receiverOptions,
            cancellationToken
        );

        Console.WriteLine("✅ Бот запущен. Нажмите Ctrl+C для завершения.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("🛑 Бот остановлен.");
        return Task.CompletedTask;
    }
}
