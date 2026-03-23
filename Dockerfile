FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы проектов
COPY ["RabbitMQ.TestApp.Web/RabbitMQ.TestApp.Web.csproj", "RabbitMQ.TestApp.Web/"]
COPY ["RabbitMQ.Module/RabbitMQ.Module.csproj", "RabbitMQ.Module/"]

# Восстанавливаем зависимости
RUN dotnet restore "RabbitMQ.TestApp.Web/RabbitMQ.TestApp.Web.csproj"

# Копируем весь исходный код
COPY . .

# Собираем приложение
WORKDIR "/src/RabbitMQ.TestApp.Web"
RUN dotnet build "RabbitMQ.TestApp.Web.csproj" -c Release -o /app/build

# Публикуем приложение
FROM build AS publish
RUN dotnet publish "RabbitMQ.TestApp.Web.csproj" -c Release -o /app/publish

# Финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RabbitMQ.TestApp.Web.dll"]
