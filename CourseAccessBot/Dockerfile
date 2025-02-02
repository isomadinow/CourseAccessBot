# 1️⃣ Используем .NET 8 SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 2️⃣ Копируем проект и восстанавливаем зависимости
COPY CourseAccessBot.csproj ./
RUN dotnet restore

# 3️⃣ Копируем все файлы и публикуем
COPY . ./
RUN dotnet publish -c Release -o out

# 4️⃣ Используем ASP.NET Core Runtime (поддерживает Microsoft.AspNetCore.App)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# 5️⃣ Копируем собранный проект
COPY --from=build /app/out ./

# 6️⃣ Копируем файлы конфигурации
COPY .env ./
COPY appsettings.json ./

# 7️⃣ Устанавливаем переменные окружения
ENV DOTNET_ENVIRONMENT=Production

# 8️⃣ Запускаем приложение
CMD ["dotnet", "CourseAccessBot.dll"]
