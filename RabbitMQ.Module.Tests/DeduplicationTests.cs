using System;
using System.Threading.Tasks;
using RabbitMQ.Module.Deduplication;
using RabbitMQ.Module.Configuration;
using RabbitMQ.Module.DeliveryControl;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Module.Tests
{
    /// <summary>
    /// Тесты для проверки работы deduplication store.
    /// </summary>
    public class DeduplicationTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly MemoryCache _cache;
        private readonly InMemoryDeduplicationStore _store;
        private readonly ILogger<InMemoryDeduplicationStore> _logger;

        public DeduplicationTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = NullLogger<InMemoryDeduplicationStore>.Instance;
            _cache = new MemoryCache(new MemoryCacheOptions());
            _store = new InMemoryDeduplicationStore(
                _cache,
                _logger,
                new DeduplicationOptions { KeyPrefix = "test:" },
                new DefaultDeliveryMetrics(NullLogger<DefaultDeliveryMetrics>.Instance));
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await _store.DisposeAsync();
        }

        [Fact]
        public async Task InMemoryStore_ShouldAddNewMessage()
        {
            // Arrange
            string messageId = "msg-1";
            TimeSpan ttl = TimeSpan.FromMinutes(5);

            // Act
            var added = await _store.TryAddAsync(messageId, ttl);

            // Assert
            Assert.True(added, "Первый ввод должен быть добавлен");
        }

        [Fact]
        public async Task InMemoryStore_ShouldDetectDuplicate()
        {
            // Arrange
            string messageId = "msg-2";
            TimeSpan ttl = TimeSpan.FromMinutes(5);

            // Act
            var added = await _store.TryAddAsync(messageId, ttl);
            var duplicate = await _store.TryAddAsync(messageId, ttl);

            // Assert
            Assert.True(added, "Первый ввод должен быть добавлен");
            Assert.False(duplicate, "Повторный ввод должен быть отклонён");
        }

        [Fact]
        public async Task InMemoryStore_ShouldCheckExists()
        {
            // Arrange
            string messageId = "msg-3";
            TimeSpan ttl = TimeSpan.FromMinutes(5);

            // Act
            var existsBefore = await _store.ExistsAsync(messageId);
            await _store.TryAddAsync(messageId, ttl);
            var existsAfter = await _store.ExistsAsync(messageId);

            // Assert
            Assert.False(existsBefore, "Сообщение не должно существовать до добавления");
            Assert.True(existsAfter, "Сообщение должно существовать после добавления");
        }

        [Fact]
        public async Task InMemoryStore_ShouldRemoveMessage()
        {
            // Arrange
            string messageId = "msg-4";
            TimeSpan ttl = TimeSpan.FromMinutes(5);
            await _store.TryAddAsync(messageId, ttl);

            // Act
            await _store.RemoveAsync(messageId);
            var exists = await _store.ExistsAsync(messageId);

            // Assert
            Assert.False(exists, "Сообщение должно быть удалено");
        }
    }
}