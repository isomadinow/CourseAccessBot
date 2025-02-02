# Указываем базовый образ .NET 8 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем csproj и восстанавливаем зависимости
COPY CourseAccessBot.csproj .
RUN dotnet restore

# Копируем остальные файлы и собираем проект
COPY . .
RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out ./

# Запуск
ENTRYPOINT ["dotnet", "CourseAccessBot.dll"]
