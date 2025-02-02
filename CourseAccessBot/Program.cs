using CourseAccessBot.Repositories;
using CourseAccessBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
                    config.SetBasePath(basePath) // Устанавливаем базовый путь
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // Подключаем appsettings.json
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((hostingContext, services) =>
                {
                    var configuration = hostingContext.Configuration;

                    // Читаем из .env
                    var botToken = Env.GetString("BOT_TOKEN");
                    if (string.IsNullOrEmpty(botToken))
                    {
                        throw new Exception("❌ Ошибка: отсутствует BOT_TOKEN в .env файле!");
                    }

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
        /// Определяет базовый путь (ищет `.env` и `appsettings.json` на 3 уровня выше).
        /// </summary>
        private static string GetBasePath()
        {
            string currentPath = Directory.GetCurrentDirectory();
            for (int i = 0; i < 3; i++) // Поднимаемся на 3 уровня выше
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
