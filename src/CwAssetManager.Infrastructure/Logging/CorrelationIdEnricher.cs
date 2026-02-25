using Serilog.Core;
using Serilog.Events;

namespace CwAssetManager.Infrastructure.Logging;

/// <summary>Serilog enricher that adds a CorrelationId property to every log event.</summary>
public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public const string CorrelationIdProperty = "CorrelationId";

    [ThreadStatic]
    private static string? _currentCorrelationId;

    /// <summary>Gets or sets the correlation ID for the current logical operation.</summary>
    public static string? Current
    {
        get => _currentCorrelationId;
        set => _currentCorrelationId = value;
    }

    /// <summary>Creates a new scope with a fresh correlation ID and returns an IDisposable to clear it.</summary>
    public static IDisposable BeginScope(string? id = null)
    {
        var previous = _currentCorrelationId;
        _currentCorrelationId = id ?? Guid.NewGuid().ToString("N")[..12];
        return new Scope(previous);
    }

    /// <inheritdoc/>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var id = _currentCorrelationId ?? "(none)";
        logEvent.AddOrUpdateProperty(
            propertyFactory.CreateProperty(CorrelationIdProperty, id));
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _previous;
        public Scope(string? previous) => _previous = previous;
        public void Dispose() => _currentCorrelationId = _previous;
    }
}
