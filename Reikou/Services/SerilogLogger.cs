using Microsoft.Extensions.Logging;
using Serilog.Events;
using System;

namespace Reikou.Services;

internal sealed class SerilogLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && Serilog.Log.IsEnabled(ToLevel(logLevel));

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                            Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
            Serilog.Log.ForContext("SourceContext", typeof(T).FullName)
                   .Write(ToLevel(logLevel), exception, "{Message}", formatter(state, exception));
    }

    private static LogEventLevel ToLevel(LogLevel level) => level switch
    {
        LogLevel.Trace       => LogEventLevel.Verbose,
        LogLevel.Debug       => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning     => LogEventLevel.Warning,
        LogLevel.Error       => LogEventLevel.Error,
        LogLevel.Critical    => LogEventLevel.Fatal,
        _                    => LogEventLevel.Debug
    };
}
