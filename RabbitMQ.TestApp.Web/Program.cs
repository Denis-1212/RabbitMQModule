using Microsoft.OpenApi;

using RabbitMQ.Module;
using RabbitMQ.TestApp.Web.Extensions;
using RabbitMQ.TestApp.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "RabbitMQ Test Web API",
            Version = "v1",
            Description = "Тестовое приложение для демонстрации RabbitMQ модуля"
        });
});

// Регистрируем хранилище
builder.Services.AddSingleton<IMessageStore, InMemoryMessageStore>();

// Регистрируем сервис отправки сообщений
builder.Services.AddScoped<IMessageService, MessageService>();

// Регистрируем RabbitMQ модуль
builder.Services.AddRabbitMQModuleWithHandlers(builder.Configuration);

// Настраиваем логирование
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

WebApplication app = builder.Build();

// Запускаем потребителей
var module = app.Services.GetRequiredService<MessagingModule>();
await module.StartConsumersAsync();
await Task.Delay(500);

// Настраиваем Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Web приложение запущено");

await app.RunAsync();
