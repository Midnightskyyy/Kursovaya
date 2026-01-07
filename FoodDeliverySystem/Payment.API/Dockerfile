# Используем официальный образ .NET 8 SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проектов
COPY ["Payment.API/Payment.API.csproj", "Payment.API/"]
COPY ["Shared.Core/Shared.Core.csproj", "Shared.Core/"]
COPY ["Shared.Messages/Shared.Messages.csproj", "Shared.Messages/"]

# Восстанавливаем зависимости
RUN dotnet restore "Payment.API/Payment.API.csproj"

# Копируем весь исходный код
COPY . .

# Переходим в папку проекта и собираем его
WORKDIR "/src/Payment.API"
RUN dotnet build "Payment.API.csproj" -c Release -o /app/build

# Публикуем приложение
FROM build AS publish
RUN dotnet publish "Payment.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Используем легковесный runtime образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Устанавливаем инструменты для отладки (опционально)
RUN apt-get update && apt-get install -y curl

# Копируем собранное приложение
COPY --from=publish /app/publish .

# Открываем порт
EXPOSE 80
EXPOSE 443

# Запускаем приложение
ENTRYPOINT ["dotnet", "Payment.API.dll"]