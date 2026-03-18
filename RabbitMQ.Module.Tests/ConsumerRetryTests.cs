namespace RabbitMQ.Module.Tests;

using Client;
using Client.Exceptions;

using Configuration;

using Contracts;

using Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.RabbitMq;

using Tools;

using Xunit.Abstractions;

public class ConsumerRetryTests : IAsyncLifetime
{

    #region Fields

    private readonly RabbitMqContainer _rabbitMqContainer;
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;
    private MessagingModule? _module;
    private IPublisher? _publisher;
    private readonly List<TestMessage> _receivedMessages = new();
    private readonly List<Exception> _thrownExceptions = new();
    private readonly SemaphoreSlim _messageSignal = new(0);
    private int _processingAttempts;
    private bool _simplyException;

    #endregion

    #region Constructors

    public ConsumerRetryTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = new TestLoggerFactory(output);

        _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3.12-management-alpine")
            .WithPortBinding(5672, true)
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
    }

    #endregion

    #region Methods

    [Fact]
    public async Task Consumer_ShouldRetry_OnFailure()
    {
        // Arrange
        ushort port = _rabbitMqContainer.GetMappedPublicPort(5672);
        _output.WriteLine($"RabbitMQ на порту: {port}");

        var services = new ServiceCollection();
        services.AddSingleton(this);
        services.AddTransient<FailingHandler>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _module = MessagingModule.Create(
                options =>
                {
                    options.ConnectionString = $"amqp://guest:guest@localhost:{port}";
                    options.ClientProvidedName = "RetryTest";
                    options.PublisherConfirms = true;
                    options.DeliveryControl.MaxRetryAttempts = 3;
                    options.DeliveryControl.RetryBaseDelayMs = 100;
                    options.DeliveryControl.RetryDelayMultiplier = 1.0;
                    options.DeliveryControl.UseRequeueForRetries = false;
                },
                _loggerFactory,
                serviceProvider)
            .AddConsumer<TestMessage, FailingHandler>(c =>
            {
                c.QueueName = "retry.test.queue";
                c.PrefetchCount = 1;
                c.Durable = false;
                c.AutoDelete = true;
            });

        _publisher = _module.CreatePublisher();

        // 1. Удаляем старую очередь
        await DeleteQueue(port, "retry.test.queue");

        // 2. Запускаем consumer (создает новую очередь)
        await _module.StartConsumersAsync();

        // 3. Ждем инициацию
        await Task.Delay(1000);

        // 4. ПЕРЕЗАПУСКАЕМ CONSUMER, чтобы очистить prefetch буфер
        await _module.StopConsumersAsync();
        await _module.StartConsumersAsync();
        await Task.Delay(1000);

        var testMessage = new TestMessage
        {
            Text = "Retry Test",
            Number = 42,
            ShouldFail = true
        };

        // Act
        _processingAttempts = 0;
        _simplyException = true;

        await _publisher.PublishAsync(
            testMessage,
            config =>
            {
                config.WithRoutingKey("retry.test.queue");
            });

        // Assert
        bool messageReceived = await _messageSignal.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(messageReceived, "Сообщение должно быть обработано после повторных попыток");
    }

    [Fact]
    public async Task Consumer_ShouldSendToDeadLetter_AfterMaxRetries()
    {
        // Arrange
        ushort port = _rabbitMqContainer.GetMappedPublicPort(5672);
        _output.WriteLine($"RabbitMQ на порту: {port}");

        var services = new ServiceCollection();
        services.AddSingleton(this);
        services.AddTransient<FailingHandler>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        string queueName = "dlq.test.queue";

        _module = MessagingModule.Create(
                options =>
                {
                    options.ConnectionString = $"amqp://guest:guest@localhost:{port}";
                    options.ClientProvidedName = "DeadLetterTest";
                    options.PublisherConfirms = true;
                    options.DeliveryControl.MaxRetryAttempts = 2; // Всего 2 попытки
                    options.DeliveryControl.RetryBaseDelayMs = 100;
                    options.DeliveryControl.EnableDeadLetter = true;
                    options.DeliveryControl.DeadLetterExchange = "dlx.test";
                    options.DeliveryControl.DeadLetterRoutingKey = "dead.letters";
                },
                _loggerFactory,
                serviceProvider)
            .AddConsumer<TestMessage, FailingHandler>(c =>
            {
                c.QueueName = queueName;
                c.PrefetchCount = 1;
                c.Durable = false;
                c.AutoDelete = true;
                // Настраиваем DLX для очереди
                c.DeadLetter = new DeadLetterOptions
                {
                    Exchange = "dlx.test",
                    RoutingKey = "dead.letters"
                };
            }).AddConsumer<TestMessage, FailingHandler>(c =>
            {
                c.QueueName = queueName;
                c.PrefetchCount = 1;
                c.Durable = false;
                c.AutoDelete = true;
            });

        _publisher = _module.CreatePublisher();
        await _module.StartConsumersAsync();
        await Task.Delay(500);

        var testMessage = new TestMessage
        {
            Text = "Dead Letter Test",
            Number = 99,
            ShouldFail = true // Всегда падает
        };

        // Act
        _processingAttempts = 0;
        _thrownExceptions.Clear();

        await _publisher.PublishAsync(
            testMessage,
            config =>
            {
                config.WithRoutingKey(queueName);
            });

        // Ждем достаточно времени для всех попыток
        await Task.Delay(3000);

        // Assert
        // Проверяем, что было 2 попытки (максимум)
        Assert.Equal(2, _processingAttempts);

        // Сообщение НЕ должно быть обработано успешно
        Assert.Empty(_receivedMessages);

        _output.WriteLine($"✅ Тест завершен: {_processingAttempts} попыток, сообщение отправлено в DLQ");

        // TODO: Проверить наличие сообщения в DLQ
        // Для этого нужен отдельный consumer для DLQ или проверка через Management API
    }

    [Fact]
    public async Task Consumer_ShouldNotRetry_OnBusinessError()
    {
        // Arrange
        ushort port = _rabbitMqContainer.GetMappedPublicPort(5672);
        _output.WriteLine($"RabbitMQ на порту: {port}");

        var services = new ServiceCollection();
        services.AddSingleton(this);
        services.AddTransient<FailingHandler>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _module = MessagingModule.Create(
                options =>
                {
                    options.DeliveryControl.EnableDeadLetter = true;
                    options.DeliveryControl.DeadLetterExchange = "dlx.test";
                    options.DeliveryControl.DeadLetterRoutingKey = "dead.letters";
                    options.ConnectionString = $"amqp://guest:guest@localhost:{port}";
                    options.ClientProvidedName = "BusinessErrorTest";
                    options.PublisherConfirms = true;
                    options.DeliveryControl.MaxRetryAttempts = 5; // Много попыток
                    options.DeliveryControl.EnableDeadLetter = true;
                },
                _loggerFactory,
                serviceProvider)
            .AddConsumer<TestMessage, FailingHandler>(c =>
            {
                c.QueueName = "business.error.queue";
                c.PrefetchCount = 1;
                c.Durable = false;
                c.AutoDelete = true;
                c.DeadLetter = new DeadLetterOptions
                {
                    Exchange = "dlx.test",
                    RoutingKey = "dead.letters"
                };
            });

        _publisher = _module.CreatePublisher();
        await _module.StartConsumersAsync();
        await Task.Delay(500);

        var testMessage = new TestMessage
        {
            Text = "Business Error Test",
            Number = 77,
            ShouldFail = true
        };

        // Модифицируем обработчик для этого теста? Или переопределяем ShouldSendToDeadLetter
        // В текущей реализации бизнес-ошибки должны отправляться сразу в DLQ

        // Act
        _processingAttempts = 0;

        await _publisher.PublishAsync(
            testMessage,
            config =>
            {
                config.WithRoutingKey("business.error.queue");
            });

        await Task.Delay(1000);

        // Assert
        // Должна быть только одна попытка (без retry)
        Assert.Equal(1, _processingAttempts);
        Assert.Empty(_receivedMessages);

        _output.WriteLine("✅ Тест завершен: только одна попытка, сообщение в DLQ");
    }

    [Fact]
    public async Task Consumer_ShouldRespectRetryDelay()
    {
        // Arrange
        ushort port = _rabbitMqContainer.GetMappedPublicPort(5672);
        _output.WriteLine($"RabbitMQ на порту: {port}");

        var services = new ServiceCollection();
        services.AddSingleton(this);
        services.AddTransient<FailingHandler>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _module = MessagingModule.Create(
                options =>
                {
                    options.ConnectionString = $"amqp://guest:guest@localhost:{port}";
                    options.ClientProvidedName = "RetryDelayTest";
                    options.PublisherConfirms = true;
                    options.DeliveryControl.MaxRetryAttempts = 3;
                    options.DeliveryControl.RetryBaseDelayMs = 500; // 500ms задержка
                    options.DeliveryControl.RetryDelayMultiplier = 2.0; // Удвоение
                    options.DeliveryControl.UseRequeueForRetries = false;
                    options.DeliveryControl.EnableDeadLetter = false;
                },
                _loggerFactory,
                serviceProvider)
            .AddConsumer<TestMessage, FailingHandler>(c =>
            {
                c.QueueName = "retry.delay.queue";
                c.PrefetchCount = 1;
                c.Durable = false;
                c.AutoDelete = true;
            });

        _publisher = _module.CreatePublisher();
        await _module.StartConsumersAsync();
        await Task.Delay(500);

        var testMessage = new TestMessage
        {
            Text = "Retry Delay Test",
            Number = 55,
            ShouldFail = true
        };

        // Act
        _processingAttempts = 0;
        _simplyException = true;
        DateTime startTime = DateTime.UtcNow;

        await _publisher.PublishAsync(
            testMessage,
            config =>
            {
                config.WithRoutingKey("retry.delay.queue");
            });

        bool messageReceived = await _messageSignal.WaitAsync(TimeSpan.FromSeconds(10));
        DateTime endTime = DateTime.UtcNow;
        TimeSpan totalTime = endTime - startTime;

        // Assert
        Assert.True(messageReceived);
        Assert.Equal(3, _processingAttempts);

        // Ожидаемое время: 0 + 500ms + 1000ms = ~1500ms (плюс накладные расходы)
        _output.WriteLine($"Общее время обработки: {totalTime.TotalMilliseconds}ms");
        Assert.True(totalTime.TotalMilliseconds > 1000, "Должно быть минимум 1000ms задержки");
        Assert.True(totalTime.TotalMilliseconds < 5000, "Не должно быть слишком долго");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("Запуск RabbitMQ...");
        await _rabbitMqContainer.StartAsync();
        _output.WriteLine("RabbitMQ запущен");
    }

    public async Task DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }

        await _rabbitMqContainer.DisposeAsync();
        _loggerFactory.Dispose();
        _simplyException = false;
    }

    private async Task DeleteQueue(int port, string queueName)
    {
        try
        {
            var connectionManager = new ConnectionManager(
                new MessagingOptions
                {
                    ConnectionString = $"amqp://guest:guest@localhost:{port}"
                },
                new NullLogger<ConnectionManager>());

            IConnection connection = await connectionManager.GetConnectionAsync();
            IChannel channel = await connection.CreateChannelAsync();

            // Удаляем очередь (с сообщениями)
            await channel.QueueDeleteAsync(queueName, false, false);
            _output.WriteLine($"Очередь {queueName} удалена");

            await channel.CloseAsync();
        }
        catch (OperationInterruptedException ex) when (ex.Message.Contains("NOT_FOUND"))
        {
            _output.WriteLine($"Очередь {queueName} не существует, пропускаем удаление");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Ошибка при удалении очереди {queueName}: {ex.Message}");
        }
    }

    #endregion

    #region Classes

    public class TestMessage
    {

        #region Properties

        public string Text { get; set; } = string.Empty;
        public int Number { get; set; }
        public bool ShouldFail { get; set; }

        #endregion

    }

    public class FailingHandler : IMessageHandler<TestMessage>
    {

        #region Fields

        private readonly ConsumerRetryTests _tests;

        #endregion

        #region Constructors

        public FailingHandler(ConsumerRetryTests tests)
        {
            _tests = tests;
        }

        #endregion

        #region Methods

        public async Task HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
        {
            _tests._processingAttempts++;
            _tests._output.WriteLine($"Обработка сообщения {message.Text}, попытка #{_tests._processingAttempts}");

            if (_tests._simplyException && _tests._processingAttempts < 3)
            {
                _tests._output.WriteLine("❌ Имитация ошибки для обработки без мертвой очереди");
                throw new TimeoutException("Simulated processing failure");
            }

            if (message.ShouldFail && _tests._processingAttempts < 3)
            {
                _tests._output.WriteLine("❌ Сообщение не обработано (имитация ошибки)");
                throw new InvalidOperationException("Simulated processing failure");
            }

            _tests._output.WriteLine("✅ Сообщение обработано успешно");
            _tests._receivedMessages.Add(message);
            await context.AckAsync(cancellationToken);
            _tests._messageSignal.Release();
        }

        #endregion

    }

    #endregion

    // private async Task PurgeQueue(int port, string queueName)
    // {
    //     try
    //     {
    //         var connectionManager = new ConnectionManager(
    //             new MessagingOptions
    //             {
    //                 ConnectionString = $"amqp://guest:guest@localhost:{port}"
    //             },
    //             new NullLogger<ConnectionManager>());
    //
    //         IConnection connection = await connectionManager.GetConnectionAsync();
    //         IChannel channel = await connection.CreateChannelAsync();
    //
    //         // Проверяем существование очереди перед очисткой
    //         try
    //         {
    //             await channel.QueueDeclarePassiveAsync(queueName);
    //             await channel.QueuePurgeAsync(queueName);
    //             _output.WriteLine($"Очередь {queueName} очищена");
    //         }
    //         catch (OperationInterruptedException ex) when (ex.Message.Contains("NOT_FOUND"))
    //         {
    //             _output.WriteLine($"Очередь {queueName} не существует, пропускаем очистку");
    //         }
    //
    //         await channel.CloseAsync();
    //     }
    //     catch (Exception ex)
    //     {
    //         _output.WriteLine($"Ошибка при очистке очереди {queueName}: {ex.Message}");
    //     }
    // }
}
