using System.Security.Cryptography;
using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities;

/// <summary>Creates category-specific runtime scopes for console, worker, and HTTP callers.</summary>
/// <param name="loggerFactory">Factory used to create the category logger.</param>
/// <param name="configuration">Options used by events in every scope.</param>
/// <param name="profiles">Complete named configurations, if registered.</param>
internal sealed class LogThisScopeFactory(
    ILoggerFactory loggerFactory,
    ILogThisConfiguration configuration,
    LogThisProfiles? profiles = null) : ILogThisScopeFactory
{
    #region Private Fields

    // Freeze custom fields once per factory; direct dictionary edits cannot inject reserved keys later.
    private readonly LogThisProfiles selections = profiles ?? new LogThisProfiles(configuration);

    #endregion

    #region Public Methods

    /// <summary>Creates a logger for the category and pushes it into the ambient runtime.</summary>
    /// <param name="categoryName">Logging category assigned to events in this scope.</param>
    /// <returns>A scope that restores the previous runtime when disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="categoryName"/> is empty or whitespace.</exception>
    public IDisposable BeginScope(string categoryName)
        => BeginScope(categoryName, static () => RandomNumberGenerator.GetString(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 16));

    #endregion

    #region Internal Methods

    /// <summary>Creates a scope with a per-call generation seam for failure tests.</summary>
    /// <param name="categoryName">Logging category.</param>
    /// <param name="generateId">Cryptographic generator, called lazily by the first eligible event in a new chain.</param>
    /// <returns>A scope that restores the previous runtime.</returns>
    internal IDisposable BeginScope(string categoryName, Func<string> generateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        LogThisRuntime? previous = LogThisRuntimeContext.Current;
        // Share even uninitialized or failed state; opening a scope never invokes the generator.
        var correlation = previous?.Correlation ?? new LogThisRuntimeContext.CorrelationState(generateId);

        LogThisRuntime runtime = new(
            loggerFactory.CreateLogger(categoryName),
            configuration,
            correlation,
            selections.Default.MessageComponents,
            selections);

        return LogThisRuntimeContext.Push(runtime);
    }

    #endregion
}
