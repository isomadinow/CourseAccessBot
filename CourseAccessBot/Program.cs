using CourseAccessBot.Repositories;
using CourseAccessBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using DotNetEnv;

namespace CourseAccessBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Определяем путь к .env и appsettings.json (3 уровня вверх)
            string basePath = GetBasePath();

            // Загружаем переменные из .env
            string envFilePath = Path.Combine(basePath, ".env");
            if (File.Exists(envFilePath))
            {
                Env.Load(envFilePath);
                Console.WriteLine("✅ Загружены переменные из .env");
            }
            else
            {
                throw new Exception($"❌ Ошибка: Файл .env не найден по пути {envFilePath}!");
            }

            // Запускаем приложение
            CreateHostBuilder(args, basePath).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args, string basePath) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(basePath)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((hostingContext, services) =>
                {
                    var configuration = hostingContext.Configuration;

                    // Читаем токен из .env
                    var botToken = Env.GetString("BOT_TOKEN");
                    if (string.IsNullOrEmpty(botToken))
                    {
                        throw new Exception("❌ Ошибка: отсутствует BOT_TOKEN в .env файле!");
                    }

                    // Читаем список админов из .env
                    var adminIds = Env.GetString("ADMIN_IDS")
                                      ?.Split(',')
                                      .Select(id => long.TryParse(id.Trim(), out var adminId) ? adminId : (long?)null)
                                      .Where(id => id.HasValue)
                                      .Select(id => id!.Value)
                                      .ToList() ?? new List<long>();

                    // Читаем пути хранения файлов из appsettings.json
                    var coursesFilePath = Path.Combine(basePath, configuration["BotConfiguration:Storage:CoursesFilePath"]);
                    var paymentsFilePath = Path.Combine(basePath, configuration["BotConfiguration:Storage:PaymentsFilePath"]);

                    // Проверяем существование файлов
                    EnsureFileExists(coursesFilePath, "[]");
                    EnsureFileExists(paymentsFilePath, "[]");

                    // Регистрируем зависимости
                    services.AddSingleton(new CourseRepository(coursesFilePath));
                    services.AddSingleton(new PaymentRepository(paymentsFilePath));
                    services.AddSingleton<ITelegramBotClient>(provider =>
                        new TelegramBotClient(botToken));

                    // Регистрируем AdminHandlers
                    services.AddSingleton<AdminHandlers>(provider =>
                    {
                        var botClient = provider.GetRequiredService<ITelegramBotClient>();
                        var courseRepo = provider.GetRequiredService<CourseRepository>();
                        var paymentRepo = provider.GetRequiredService<PaymentRepository>();

                        return new AdminHandlers(botClient, courseRepo, paymentRepo, adminIds);
                    });

                    // Регистрируем UserHandlers
                    services.AddSingleton<UserHandlers>(provider =>
                    {
                        var botClient = provider.GetRequiredService<ITelegramBotClient>();
                        var courseRepo = provider.GetRequiredService<CourseRepository>();
                        var paymentRepo = provider.GetRequiredService<PaymentRepository>();

                        return new UserHandlers(botClient, courseRepo, paymentRepo);
                    });

                    // Регистрируем BotHandlers
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

        private static string GetBasePath()
        {
            string currentPath = Directory.GetCurrentDirectory();
            for (int i = 0; i < 3; i++)
            {
                currentPath = Directory.GetParent(currentPath)!.FullName;
            }
            return currentPath;
        }

        private static void EnsureFileExists(string filePath, string defaultContent)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new Exception("❌ Ошибка: Путь к файлу данных не указан в appsettings.json!");
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, defaultContent);
                Console.WriteLine($"✅ Создан файл: {filePath}");
            }
        }
    }
}
