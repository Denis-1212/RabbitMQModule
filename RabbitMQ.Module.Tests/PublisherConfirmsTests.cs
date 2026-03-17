namespace RabbitMQ.Module.Tests;

using Contracts;

using Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Testcontainers.RabbitMq;

using Tools;

using Xunit.Abstractions;

public class PublisherConfirmsTests(ITestOutputHelper output) : IAsyncLifetime
{

    #region Fields

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3.12-management-alpine")
        .WithPortBinding(5672, true)
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private readonly ILoggerFactory _loggerFactory = new TestLoggerFactory(output);
    private MessagingModule? _module;
    private readonly SemaphoreSlim _messageSignal = new(0);
    private readonly List<TestMessage> _receivedMessages = [];

    #endregion

    #region Methods

    [Fact]
    public async Task PublishAsync_WithConfirmsEnabled_ShouldWaitForConfirmation()
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

        var publisher = _module.CreatePublisher() as Publisher;
        publisher?.ResetStats();

        var testMessage = new TestMessage
        {
            Text = "Hello",
            Number = 42
        };

        await _module.StartConsumersAsync();

        // Act
        Exception? exception = await Record.ExceptionAsync(async () =>
        {
            if (publisher != null)
            {
                await publisher.PublishAsync(
                    testMessage,
                    config =>
                    {
                        config.WithRoutingKey("test.queue");
                        config.WithMandatory();
                    });
            }
        });

        // Assert
        Assert.True(publisher?.LastPublishWasConfirmed, "Сообщение должно быть подтверждено");
        Assert.NotNull(publisher?.LastConfirmLatency);
        Assert.True(
            publisher.LastConfirmLatency.Value.TotalMilliseconds < 1000,
            "Подтверждение должно прийти быстро");

        bool messageReceived = await _messageSignal.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(messageReceived, "Сообщение не было получено");
        Assert.Single(_receivedMessages);
        Assert.Equal("Hello", _receivedMessages[0].Text);
        Assert.Equal(42, _receivedMessages[0].Number);
        Assert.Null(exception);
    }

    public async Task InitializeAsync()
    {
        output.WriteLine("Запуск RabbitMQ...");
        await _rabbitMqContainer.StartAsync();
        output.WriteLine("RabbitMQ запущен");
    }

    public async Task DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }

        await _rabbitMqContainer.DisposeAsync();
        _loggerFactory.Dispose();
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

    public class TestHandler(PublisherConfirmsTests tests) : IMessageHandler<TestMessage>
    {

        #region Methods

        public Task HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
        {
            tests._receivedMessages.Add(message);
            tests._messageSignal.Release();
            return Task.CompletedTask;
        }

        #endregion

    }

    #endregion

}
