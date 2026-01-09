FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проектов
COPY ["Delivery.API/Delivery.API.csproj", "Delivery.API/"]
COPY ["Shared.Core/Shared.Core.csproj", "Shared.Core/"]
COPY ["Shared.Messages/Shared.Messages.csproj", "Shared.Messages/"]

# Восстанавливаем зависимости
RUN dotnet restore "Delivery.API/Delivery.API.csproj"

# Копируем весь исходный код
COPY . .

# Переходим в папку проекта и собираем его
WORKDIR "/src/Delivery.API"
RUN dotnet build "Delivery.API.csproj" -c Release -o /app/build

# Публикуем приложение
FROM build AS publish
RUN dotnet publish "Delivery.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Используем легковесный runtime образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Устанавливаем curl для health checks
RUN apt-get update && apt-get install -y curl

# Копируем собранное приложение
COPY --from=publish /app/publish .

# Открываем порты
EXPOSE 80
EXPOSE 443

# Запускаем приложение
ENTRYPOINT ["dotnet", "Delivery.API.dll"]