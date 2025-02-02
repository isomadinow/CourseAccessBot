
# 📌 CourseAccessBot

**CourseAccessBot** — Телеграм-бот для управления курсами, загрузки чеков оплаты и проверки администраторами.

## 🚀 Возможности
- 📚 Просмотр списка курсов
- 📩 Отправка чека об оплате (фото/PDF)
- ⏳ Проверка статуса оплаты
- ✅ Получение ссылки на курс после подтверждения
- ℹ️ Команды **Помощь** и **Контакты**
- 🛠 Административные функции:
  - ➕ Добавление/удаление курсов
  - 🔍 Проверка поступивших оплат
  - ⚡ Подтверждение/отклонение оплат

---

## 🔧 Установка и запуск

### 🏗 1. Склонируйте репозиторий:
```bash
git clone https://github.com/isomadinow/CourseAccessBot.git
cd CourseAccessBot
```

### 📦 2. Установите зависимости:
```bash
dotnet restore
```

### 🏃 3. Запустите бота:
```bash
dotnet run
```

---

## 🛠 Переменные окружения

Перед запуском настройте **.env** файл:

```
BOT_TOKEN=your_telegram_bot_token
ADMIN_IDS=123456789,987654321
```

- **BOT_TOKEN** — токен бота из [BotFather](https://t.me/BotFather)
- **ADMIN_IDS** — список ID администраторов через запятую

---

## 🐳 Запуск через Docker

### 📌 **Сборка образа**
```bash
docker build -t courseaccessbot .
```

### 🚀 **Запуск контейнера**
```bash
docker run --env-file .env -d --name courseaccessbot courseaccessbot
```

### 🔄 **Логи**
```bash
docker logs -f courseaccessbot
```

---

## 📜 Структура проекта

```
CourseAccessBot/
├── Models/              # Модели данных
├── Repositories/        # Репозитории (работа с файлами)
├── Services/            # Основная логика бота
├── Program.cs           # Точка входа
├── appsettings.json     # Настройки бота
├── .env                 # Переменные окружения
├── Dockerfile           # Конфигурация Docker
└── README.md            # Документация
```

---

## 🤝 Контакты
- **Автор:** Azizkhon Isomadinov  
- **Telegram:** [@a_khurshedhonovich08](https://t.me/a_khurshedhonovich08)  
- **Лицензия:** MIT

