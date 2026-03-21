# RabbitMQ Module - модуль для работы с RabbitMQ

## Общее описание:

RabbitMQ Module — это .NET модуль, предоставляющий простой и интуитивно понятный API для работы с RabbitMQ. Модуль инкапсулирует сложности работы с брокером сообщений, предоставляя высокоуровневый интерфейс с гибкими настройками для промышленных сценариев использования.

## Основные возможности:

1. ### Простая инициализация
  
 ```
var module = MessagingModule.Create(options =>
 {    options.ConnectionString = "amqp://guest:guest@localhost:5672/";
      options.ClientProvidedName = "MyApplication";}
);
```

2. ### Публикация сообщений

```
var publisher = module.CreatePublisher();

// Простая публикация
await publisher.PublishAsync(new OrderCreated { OrderId = 123 });

// Публикация с дополнительными настройками
await publisher.PublishAsync(new OrderCreated { OrderId = 123 }, config =>
{
    config.WithRoutingKey("orders.new");
    config.WithExchange("orders.exchange");
    config.WithPriority(5);
    config.WithExpiration(TimeSpan.FromMinutes(5));
    config.WithHeader("source", "web-api");
});

```

3. ### Потребление сообщений

```
public class OrderHandler : IMessageHandler<OrderCreated>
{
    private readonly ILogger<OrderHandler> _logger;

    public OrderHandler(ILogger<OrderHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreated message, IMessageContext context, CancellationToken cancellationToken)
    {
        // Бизнес-логика
        await ProcessOrder(message);
        
        // Подтверждение обработки
        await context.AckAsync();
    }
}

// Регистрация потребителя
module.AddConsumer<OrderCreated, OrderHandler>(c =>
{
    c.QueueName = "orders.new";
    c.PrefetchCount = 1;
    c.Durable = true;
});
```

 4. ### Гарантированная доставка (Publisher Confirms)

```
options.DeliveryControl.PublisherConfirmsEnabled = true;
options.DeliveryControl.PublishConfirmationTimeoutMs = 5000;
```
- Автоматическое ожидание подтверждения от брокера
- Таймауты при недоступности брокера
- Автоматические повторные попытки при сбоях

5. ### Контроль доставки (Retry Logic)

```
options.DeliveryControl.MaxRetryAttempts = 3;
options.DeliveryControl.RetryBaseDelayMs = 1000;
options.DeliveryControl.RetryDelayMultiplier = 2.0;
```
- Экспоненциальная задержка между попытками
- Настраиваемое количество попыток
- Автоматическое возвращение сообщений в очередь при ошибках

6. ### Dead Letter Queue (DLQ)

```
options.DeliveryControl.EnableDeadLetter = true;
options.DeliveryControl.DeadLetterExchange = "dlx";
options.DeliveryControl.DeadLetterRoutingKey = "dead.letters";

// Настройка DLQ для конкретной очереди
c.DeadLetter = new DeadLetterOptions
{
    Exchange = "dlx.orders",
    RoutingKey = "dead.orders",
    MaxRetries = 3
};
```
- Автоматическая отправка проблемных сообщений в DLQ
- Разделение бизнес-ошибок и технических сбоев
- Возможность анализа и восстановления

7. ### Дедубликация сообщений (в разработке)

    - In-memory и Redis хранилища
    - Гарантия exactly-once processing
    - Защита от повторной обработки

8. ### Управление подключениями

- Автоматическое восстановление соединения
- Пул каналов для эффективного использования ресурсов
- Health checks и мониторинг

9. ### Гибкая конфигурация

```
options.ConnectionString = "amqp://user:pass@host:5672/vhost";
options.ClientProvidedName = "MyApp";
options.MaxChannelsInPool = 50;
options.HeartbeatInterval = TimeSpan.FromSeconds(60);
options.AutomaticRecoveryEnabled = true;
options.NetworkRecoveryInterval = TimeSpan.FromSeconds(5);
```

10. ### Поддержка ASP.NET Core

```
// Program.cs
services.AddRabbitMQModule(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("RabbitMQ");
    options.DeliveryControl.PublisherConfirmsEnabled = true;
}, module =>
{
    module.AddConsumer<OrderCreated, OrderHandler>(c => c.QueueName = "orders.new");
});

// В контроллере
public class OrdersController : ControllerBase
{
    private readonly IPublisher _publisher;
    
    public OrdersController(IPublisher publisher)
    {
        _publisher = publisher;
    }
    
    [HttpPost]
    public async Task<IActionResult> Create(OrderDto dto)
    {
        await _publisher.PublishAsync(new OrderCreated { ... });
        return Ok();
    }
}
```

11. ### Метрики и мониторинг

```
public interface IDeliveryMetrics
{
    void MessageProcessed(string messageType, TimeSpan duration, bool success);
    void MessageRetried(string messageType, int attempt);
    void MessageDeadLettered(string messageType, string reason);
}
```

- Встроенный интерфейс для сбора метрик
- Логирование всех операций
- Возможность интеграции с Prometheus, Application Insights и другими системами

## Архитектура
```
┌─────────────────────────────────────────────────────────────┐
│                     Клиентское приложение                   │
│                     (ASP.NET Core / Console)                │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     RabbitMQ Module                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  Publisher  │  │  Consumer   │  │  MessageDispatcher  │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ ChannelPool │  │ConnectionMgr│  │   DeliveryControl   │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                       RabbitMQ Broker                       │
└─────────────────────────────────────────────────────────────┘
```

## Технологический стек

  - .NET 8 / 10 — целевая платформа
  - RabbitMQ.Client 7.x — официальный клиент
  - Polly — политики повторных попыток
  - Newtonsoft.Json — сериализация по умолчанию
  - Microsoft.Extensions.* — логирование, DI, хостинг

## Требования к окружению

   - .NET 8 SDK или выше
   - RabbitMQ 3.12+ (локально или в Docker)

## Тестирование

### Проект включает:

   - Модульные тесты
   - Интеграционные тесты с Testcontainers
   - Тестовые приложения (консольное и веб(в разработке))

## Дальнейшие планы

  - Дедубликация сообщений (In-memory, Redis)
  - Поддержка транзакций
  - Метрики для Prometheus
  - Health checks для ASP.NET Core
  - Готовые шаблоны для MassTransit

### Контакты 
d.v.salnikoff@gvail.com






















