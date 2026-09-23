using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities;

internal sealed class LogThisScopeFactory(
    ILoggerFactory loggerFactory,
    ILogThisConfiguration configuration) : ILogThisScopeFactory
{
    public IDisposable BeginScope(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        LogThisRuntime runtime = new(
            loggerFactory.CreateLogger(categoryName),
            configuration,
            configuration.GetComponentBuilders());

        return LogThisRuntimeContext.Push(runtime);
    }
}
