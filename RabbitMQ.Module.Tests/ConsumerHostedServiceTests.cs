namespace RabbitMQ.Module.Tests;

using Contracts;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Testcontainers.RabbitMq;

using Xunit.Abstractions;

public class ConsumerHostedServiceTests : IAsyncLifetime
{

    #region Fields

    private readonly RabbitMqContainer _rabbitMqContainer;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITestOutputHelper _output;
    private MessagingModule? _module;
    private IPublisher? _publisher;
    private readonly List<TestMessage> _receivedMessages = new();
    private readonly SemaphoreSlim _messageSignal = new(0);
    private readonly List<string> _logs = new();

    #endregion

    #region Constructors

    public ConsumerHostedServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = new TestLoggerFactory(output, _logs);

        _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3.12-management-alpine")
            .WithPortBinding(5672, true)
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
    }

    #endregion

    #region Methods

    [Fact]
    public async Task Consumer_ShouldReceiveMessage_WhenPublished()
    {
        // Arrange
        ushort port = _rabbitMqContainer.GetMappedPublicPort(5672);
        _output.WriteLine($"RabbitMQ запущен на порту: {port}");

        string connectionString = $"amqp://guest:guest@localhost:{port}";
        _output.WriteLine($"Строка подключения: {connectionString}");

        // Создаем DI контейнер для теста
        var services = new ServiceCollection();
        services.AddSingleton(this); // Регистрируем текущий тестовый класс
        services.AddTransient<TestHandler>(); // Регистрируем обработчик
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _module = MessagingModule.Create(
                options =>
                {
                    options.ConnectionString = connectionString;
                    options.ClientProvidedName = "TestConsumer";
                    options.PublisherConfirms = true;
                },
                _loggerFactory,
                serviceProvider)
            .AddConsumer<TestMessage, TestHandler>(c =>
            {
                c.QueueName = "test.queue";
                c.PrefetchCount = 1;
                c.Durable = false;
                c.AutoDelete = true;
            });

        _publisher = _module.CreatePublisher();

        _output.WriteLine("Запуск потребителей...");
        await _module.StartConsumersAsync();
        _output.WriteLine("Потребители запущены");

        await Task.Delay(500);

        var testMessage = new TestMessage
        {
            Text = "Hello",
            Number = 42
        };

        _output.WriteLine($"Публикация сообщения: {testMessage.Text} #{testMessage.Number}");

        // Act
        await _publisher.PublishAsync(
            testMessage,
            config =>
            {
                config.WithRoutingKey("test.queue");
            });

        _output.WriteLine("Ожидание получения сообщения...");

        // Assert
        bool received = await _messageSignal.WaitAsync(TimeSpan.FromSeconds(10));

        _output.WriteLine("Логи за время теста:");

        foreach (string log in _logs)
        {
            _output.WriteLine($"  {log}");
        }

        Assert.True(received, "Сообщение не было получено в течение 10 секунд");
        Assert.Single(_receivedMessages);
        Assert.Equal("Hello", _receivedMessages[0].Text);
        Assert.Equal(42, _receivedMessages[0].Number);

        _output.WriteLine("Тест успешно завершен");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("Запуск RabbitMQ контейнера...");
        await _rabbitMqContainer.StartAsync();
        _output.WriteLine("RabbitMQ контейнер запущен");
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine("Очистка ресурсов...");

        if (_module != null)
        {
            await _module.DisposeAsync();
        }

        await _rabbitMqContainer.DisposeAsync();
        _loggerFactory.Dispose();
        _output.WriteLine("Очистка завершена");
    }

    #endregion

    #region Classes

    public class TestMessage
    {

        #region Properties

        public string Text { get; set; } = string.Empty;
        public int Number { get; set; }

        #endregion

    }

    public class TestHandler(ConsumerHostedServiceTests tests) : IMessageHandler<TestMessage>
    {

        #region Methods

        public Task HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
        {
            tests._output.WriteLine($"Handler получил сообщение: {message.Text} #{message.Number}");
            tests._receivedMessages.Add(message);
            tests._messageSignal.Release();
            return Task.CompletedTask;
        }

        #endregion

    }

    #endregion

}

public class TestLoggerFactory(ITestOutputHelper output, List<string> logs) : ILoggerFactory
{

    #region Methods

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(categoryName, output, logs);
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    #endregion

    #region Classes

    private class TestLogger(string categoryName, ITestOutputHelper output, List<string> logs) : ILogger
    {

        #region Methods

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = $"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{categoryName}] {formatter(state, exception)}";
            logs.Add(message);
            output.WriteLine(message);
        }

        #endregion

    }

    #endregion

}
