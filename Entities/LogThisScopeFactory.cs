using System.Security.Cryptography;
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
    #region Private Fields

    // Freeze custom fields once per factory; direct dictionary edits cannot inject reserved keys later.
    private readonly Dictionary<string, object> messageComponents = LogParameters.PrepareComponents(configuration.MessageComponents);

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
    /// <param name="generateId">Cryptographic ID generator, called only for enabled outermost scopes.</param>
    /// <returns>A scope that restores the previous runtime.</returns>
    internal IDisposable BeginScope(string categoryName, Func<string> generateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        LogThisRuntime? previous = LogThisRuntimeContext.Current;
        string? correlationId = previous?.CorrelationId;
        // An existing scope with no ID is still an existing correlation chain.
        if (previous == null)
        {
            try
            {
                if (configuration.LogCorrelationId) correlationId = generateId();
            }
            catch (Exception exception)
            {
                LogThisRuntimeLogger.ReportFailure(configuration, exception,
                    "LogThis failed while generating a correlation ID.");
            }
        }

        LogThisRuntime runtime = new(
            loggerFactory.CreateLogger(categoryName),
            configuration,
            correlationId,
            messageComponents);

        return LogThisRuntimeContext.Push(runtime);
    }

    #endregion
}
