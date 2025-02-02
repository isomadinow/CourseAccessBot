using CourseAccessBot.Repositories;
using CourseAccessBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using Telegram.Bot;

namespace CourseAccessBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Создаём и запускаем хост
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    string basePath = GetBasePath(); // Получаем базовый путь (4 уровня вверх)
                    config.SetBasePath(basePath)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((hostingContext, services) =>
                {
                    var configuration = hostingContext.Configuration;

                    // Читаем конфигурацию
                    var botToken = configuration["BotConfiguration:BotToken"];
                    if (string.IsNullOrEmpty(botToken))
                    {
                        throw new Exception("❌ Ошибка: отсутствует BotToken в appsettings.json!");
                    }

                    var adminIds = configuration.GetSection("BotConfiguration:AdminIds")
                                                .Get<long[]>()?.ToList() ?? new List<long>();

                    var dataPath = Path.Combine(GetBasePath(), "Data");
                    var coursesFilePath = Path.Combine(dataPath, "courses.json");
                    var paymentsFilePath = Path.Combine(dataPath, "payments.json");

                    // Проверяем, существуют ли файлы
                    EnsureFileExists(coursesFilePath, "[]");
                    EnsureFileExists(paymentsFilePath, "[]");

                    // Регистрируем зависимости
                    services.AddSingleton(new CourseRepository(coursesFilePath));
                    services.AddSingleton(new PaymentRepository(paymentsFilePath));
                    services.AddSingleton<ITelegramBotClient>(provider =>
                        new Telegram.Bot.TelegramBotClient(botToken));

                    // Регистрируем Handlers
                    services.AddSingleton<UserHandlers>();
                    services.AddSingleton<AdminHandlers>();
                    services.AddSingleton<BotHandlers>(provider =>
                    {
                        var botClient = provider.GetRequiredService<ITelegramBotClient>();
                        var userHandlers = provider.GetRequiredService<UserHandlers>();
                        var adminHandlers = provider.GetRequiredService<AdminHandlers>();
                        return new BotHandlers(botClient, userHandlers, adminHandlers, adminIds);
                    });

                    // Регистрируем и запускаем BotService
                    services.AddHostedService<BotService>();
                });

        /// <summary>
        /// Определяет базовый путь (ищет `appsettings.json` на 4 уровня выше).
        /// </summary>
        private static string GetBasePath()
        {
            string currentPath = Directory.GetCurrentDirectory();
            for (int i = 0; i < 3; i++) // Поднимаемся на 4 уровня выше
            {
                currentPath = Directory.GetParent(currentPath)!.FullName;
            }
            return currentPath;
        }

        /// <summary>
        /// Проверяет существование файла, если нет — создаёт с указанным содержимым.
        /// </summary>
        private static void EnsureFileExists(string filePath, string defaultContent)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory); // Создаём папку, если её нет
            }

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, defaultContent); // Создаём файл с дефолтным содержимым
                Console.WriteLine($"✅ Создан файл: {filePath}");
            }
        }
    }
}
