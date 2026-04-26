using Microsoft.Extensions.Logging;
using System;

namespace PotopopiCamSync.Services
{
    public class FileLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new FileLoggerInstance(categoryName);
        }

        public void Dispose() { }

        private class FileLoggerInstance : ILogger
        {
            private readonly string _categoryName;

            public FileLoggerInstance(string categoryName)
            {
                _categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                
                string msg = formatter(state, exception);
                if (exception != null) msg += $"\n{exception}";
                
                FileLogger.Log($"[{logLevel}] [{_categoryName}] {msg}");
            }
        }
    }
}
