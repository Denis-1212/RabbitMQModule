namespace RabbitMQ.Module.Tests;

using Contracts;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Testcontainers.RabbitMq;

using Tools;

using Xunit.Abstractions;

public class ConsumerDeliveryTagTests : IAsyncLifetime
{

    #region Fields

    private readonly RabbitMqContainer _rabbitMqContainer;
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;
    private MessagingModule? _module;
    private IPublisher? _publisher;
    private readonly List<TestMessage> _receivedMessages = new();
    private readonly SemaphoreSlim _messageSignal = new(0);
    private int _processedCount;
    private int _failedCount;

    #endregion

    #region Constructors

    public ConsumerDeliveryTagTests(ITestOutputHelper output)
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
    public async Task Consumer_ShouldHandleMultipleMessages_WithDeliveryTags()
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
                    options.ClientProvidedName = "DeliveryTagTest";
                    options.PublisherConfirms = true;
                    options.DeliveryControl.MaxRetryAttempts = 1; // Одна попытка
                    options.DeliveryControl.UseRequeueForRetries = false;
                    options.DeliveryControl.EnableDeadLetter = false;
                },
                _loggerFactory,
                serviceProvider)
            .AddConsumer<TestMessage, FailingHandler>(c =>
            {
                c.QueueName = "delivery.tag.queue";
                c.PrefetchCount = 1;
                c.Durable = false;
                c.AutoDelete = true;
            });

        _publisher = _module.CreatePublisher();
        await _module.StartConsumersAsync();
        await Task.Delay(1000);

        // Act - отправляем два сообщения
        _processedCount = 0;

        _output.WriteLine("📤 Отправка первого сообщения (должно упасть)");
        await _publisher.PublishAsync(
            new TestMessage
            {
                Text = "Message 1",
                Number = 1,
                ShouldFail = true
            },
            config => config.WithRoutingKey("delivery.tag.queue"));

        _output.WriteLine("📤 Отправка второго сообщения (должно обработаться)");
        await _publisher.PublishAsync(
            new TestMessage
            {
                Text = "Message 2",
                Number = 2,
                ShouldFail = false
            },
            config => config.WithRoutingKey("delivery.tag.queue"));

        // Assert
        bool messageReceived = await _messageSignal.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(messageReceived, "Второе сообщение должно быть получено");
        Assert.Single(_receivedMessages);
        Assert.Equal("Message 2", _receivedMessages[0].Text);
        Assert.Equal(2, _receivedMessages[0].Number);
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

        private readonly ConsumerDeliveryTagTests _tests;

        #endregion

        #region Constructors

        public FailingHandler(ConsumerDeliveryTagTests tests)
        {
            _tests = tests;
        }

        #endregion

        #region Methods

        public async Task HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
        {
            _tests._processedCount++;
            _tests._output.WriteLine($"Обработка сообщения {message.Text}, попытка #{_tests._processedCount}");

            if (message.ShouldFail && _tests._processedCount == 1)
            {
                _tests._output.WriteLine("❌ Сообщение не обработано (имитация ошибки)");
                throw new InvalidOperationException("Simulated processing failure");
            }

            _tests._output.WriteLine("✅ Сообщение обработано успешно");
            await context.AckAsync(cancellationToken);
            _tests._receivedMessages.Add(message);
            _tests._messageSignal.Release();
        }

        #endregion

    }

    #endregion

}
