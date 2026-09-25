using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities;

/// <summary>Creates category-specific runtime scopes for console, worker, and HTTP callers.</summary>
/// <param name="loggerFactory">Factory used to create the category logger.</param>
/// <param name="configuration">Options used by events in every scope.</param>
internal sealed class LogThisScopeFactory(
    ILoggerFactory loggerFactory,
    ILogThisConfiguration configuration) : ILogThisScopeFactory
{
    #region Public Methods

    /// <summary>Creates a logger for the category and pushes it into the ambient runtime.</summary>
    /// <param name="categoryName">Logging category assigned to events in this scope.</param>
    /// <returns>A scope that restores the previous runtime when disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="categoryName"/> is empty or whitespace.</exception>
    public IDisposable BeginScope(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        LogThisRuntime runtime = new(
            loggerFactory.CreateLogger(categoryName),
            configuration);

        return LogThisRuntimeContext.Push(runtime);
    }

    #endregion
}
