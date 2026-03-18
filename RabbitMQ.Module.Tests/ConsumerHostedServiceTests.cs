namespace RabbitMQ.Module.Tests;

using Contracts;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Testcontainers.RabbitMq;

using Tools;

using Xunit.Abstractions;

public class ConsumerHostedServiceTests(ITestOutputHelper output) : IAsyncLifetime
{

    #region Fields

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3.12-management-alpine")
        .WithPortBinding(5672, true)
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private readonly ILoggerFactory _loggerFactory = new TestLoggerFactory(output);
    private MessagingModule? _module;
    private IPublisher? _publisher;
    private readonly List<TestMessage> _receivedMessages = [];
    private readonly SemaphoreSlim _messageSignal = new(0);

    #endregion

    #region Methods

    [Fact]
    public async Task Consumer_ShouldReceiveMessage_WhenPublished()
    {
        // Arrange
        ushort port = _rabbitMqContainer.GetMappedPublicPort(5672);
        output.WriteLine($"RabbitMQ запущен на порту: {port}");

        string connectionString = $"amqp://guest:guest@localhost:{port}";
        output.WriteLine($"Строка подключения: {connectionString}");

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

        await _module.StartConsumersAsync();
        output.WriteLine("Потребители запущены");

        await Task.Delay(500);

        var testMessage = new TestMessage
        {
            Text = "Hello",
            Number = 42
        };

        // Act
        await _publisher.PublishAsync(
            testMessage,
            config =>
            {
                config.WithRoutingKey("test.queue");
            });

        // Assert
        bool messageReceived = await _messageSignal.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(messageReceived, "Сообщение не было получено");
        Assert.Single(_receivedMessages);
        Assert.Equal("Hello", _receivedMessages[0].Text);
        Assert.Equal(42, _receivedMessages[0].Number);
    }

    public async Task InitializeAsync()
    {
        output.WriteLine("Запуск RabbitMQ контейнера...");
        await _rabbitMqContainer.StartAsync();
        output.WriteLine("RabbitMQ контейнер запущен");
    }

    public async Task DisposeAsync()
    {
        output.WriteLine("Очистка ресурсов...");

        if (_module != null)
        {
            await _module.DisposeAsync();
        }

        await _rabbitMqContainer.DisposeAsync();
        _loggerFactory.Dispose();
        output.WriteLine("Очистка завершена");
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

        public async Task HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
        {
            tests._receivedMessages.Add(message);
            tests._messageSignal.Release();
            await context.AckAsync(cancellationToken);
        }

        #endregion

    }

    #endregion

}
