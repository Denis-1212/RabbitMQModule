namespace RabbitMQ.Module.Tests.Tools;

using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

public class TestLoggerFactory(ITestOutputHelper output) : ILoggerFactory
{

    #region Methods

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(categoryName, output);
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    #endregion

    #region Classes

    private class TestLogger(string categoryName, ITestOutputHelper output) : ILogger
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
            output.WriteLine(message);
        }

        #endregion

    }

    #endregion

}
