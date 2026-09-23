using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities;

internal sealed record LogThisRuntime(
    ILogger Logger,
    ILogThisConfiguration Configuration,
    IReadOnlyList<IMessageComponentBuilder> ComponentBuilders);

internal static class LogThisRuntimeContext
{
    private static readonly AsyncLocal<LogThisRuntime?> CurrentRuntime = new();

    internal static LogThisRuntime? Current => CurrentRuntime.Value;

    internal static IDisposable Push(LogThisRuntime runtime)
    {
        LogThisRuntime? previous = CurrentRuntime.Value;
        CurrentRuntime.Value = runtime;
        return new RuntimeScope(() => CurrentRuntime.Value = previous);
    }

    private sealed class RuntimeScope(Action restore) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            restore();
        }
    }
}
