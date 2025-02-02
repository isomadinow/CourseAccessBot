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
            // Определяем окружение: Production / Development
            string environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            Console.WriteLine($"🚀 Запуск в режиме: {environment}");

            // Определяем базовый путь
            string basePath = GetBasePath();

            // Загружаем переменные из .env, если это Development
            string envFilePath = Path.Combine(basePath, ".env");
            if (environment == "Development" && File.Exists(envFilePath))
            {
                Env.Load(envFilePath);
                Console.WriteLine("✅ Загружены переменные из .env");
            }

            // Запускаем приложение
            CreateHostBuilder(args, basePath, environment).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args, string basePath, string environment) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(basePath)
                          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((hostingContext, services) =>
                {
                    var configuration = hostingContext.Configuration;

                    // Читаем BOT_TOKEN из окружения
                    var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? Env.GetString("BOT_TOKEN", "");
                    if (string.IsNullOrEmpty(botToken))
                    {
                        throw new Exception("❌ Ошибка: BOT_TOKEN отсутствует!");
                    }

                    // Читаем список админов
                    var adminIds = (Environment.GetEnvironmentVariable("ADMIN_IDS") ?? Env.GetString("ADMIN_IDS", ""))
                                      .Split(',')
                                      .Select(id => long.TryParse(id.Trim(), out var adminId) ? adminId : (long?)null)
                                      .Where(id => id.HasValue)
                                      .Select(id => id!.Value)
                                      .ToList() ?? new List<long>();

                    // Читаем пути хранения файлов
                    var coursesFilePath = Path.Combine(basePath, configuration["BotConfiguration:Storage:CoursesFilePath"] ?? "courses.json");
                    var paymentsFilePath = Path.Combine(basePath, configuration["BotConfiguration:Storage:PaymentsFilePath"] ?? "payments.json");

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

        /// <summary>
        /// Определяет базовый путь
        /// </summary>
        private static string GetBasePath()
        {
            string currentPath = Directory.GetCurrentDirectory();

            // Проверяем, существует ли appsettings.json
            if (File.Exists(Path.Combine(currentPath, "appsettings.json")))
            {
                return currentPath;
            }

            // В Docker остаёмся в корневой директории
            return currentPath;
        }

        /// <summary>
        /// Проверяет существование файла, если нет — создаёт с указанным содержимым.
        /// </summary>
        private static void EnsureFileExists(string filePath, string defaultContent)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new Exception("❌ Ошибка: Путь к файлу данных не указан!");
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
