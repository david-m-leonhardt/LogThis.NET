using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities;

/// <summary>The logger and configuration selected for one active logging scope.</summary>
/// <param name="Logger">Logger created for the scope's category.</param>
/// <param name="Configuration">Options shared by events in the scope.</param>
/// <param name="Correlation">Shared lazy correlation state for the entire operation chain.</param>
/// <param name="MessageComponents">Validated custom fields captured when the scope factory was created.</param>
/// <param name="Profiles">Default and named configurations for the operation's provider.</param>
internal sealed record LogThisRuntime(
    ILogger Logger,
    ILogThisConfiguration Configuration,
    LogThisRuntimeContext.CorrelationState Correlation,
    Dictionary<string, object> MessageComponents,
    LogThisProfiles Profiles)
{
    /// <summary>The ID if already generated; reading this property never generates one.</summary>
    internal string? CorrelationId => Correlation.Id;
}

/// <summary>Flows the current runtime through asynchronous execution without global reinitialization.</summary>
internal static class LogThisRuntimeContext
{
    #region Internal Properties

    /// <summary>The runtime active in the current asynchronous flow, if any.</summary>
    /// <value>The current runtime, or <see langword="null"/> outside a LogThis scope.</value>
    internal static LogThisRuntime? Current => CurrentRuntime.Value;

    #endregion

    #region Private Properties

    // AsyncLocal keeps concurrent requests or worker operations isolated.
    private static readonly AsyncLocal<LogThisRuntime?> CurrentRuntime = new();

    #endregion

    #region Internal Methods

    /// <summary>Activates a runtime and returns a scope that restores the previous one.</summary>
    /// <param name="runtime">Runtime to make current for this asynchronous flow.</param>
    /// <returns>A scope that restores the previous runtime when disposed.</returns>
    internal static IDisposable Push(LogThisRuntime runtime)
    {
        // Nested scopes restore the previous runtime when disposed.
        LogThisRuntime? previous = CurrentRuntime.Value;
        CurrentRuntime.Value = runtime;
        return new RuntimeScope(() => CurrentRuntime.Value = previous);
    }

    #endregion

    #region Private Class

    /// <summary>Restores the previous runtime exactly once on disposal.</summary>
    /// <param name="restore">Action that restores the previous runtime.</param>
    private sealed class RuntimeScope(Action restore) : IDisposable
    {
        #region Private Properties

        // Prevents repeated disposal from restoring an outdated scope.
        private bool disposed;

        #endregion

        #region Public Method

        /// <summary>Restores the runtime that preceded this scope.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            restore();
        }

        #endregion
    }

    #endregion

    #region Internal Class

    /// <summary>Coordinates one cryptographic generation attempt across all scopes and methods in a chain.</summary>
    internal sealed class CorrelationState(Func<string> generateId)
    {
        #region Private Fields

        // ExecutionAndPublication shares a value or caches a failure, without retrying the generator.
        private readonly Lazy<string> id = new(generateId, LazyThreadSafetyMode.ExecutionAndPublication);
        private int failureReported;

        #endregion

        #region Internal Properties

        /// <summary>Observes a completed ID without triggering generation.</summary>
        internal string? Id => id.IsValueCreated ? id.Value : null;

        #endregion

        #region Internal Methods

        /// <summary>Obtains the shared ID for an eligible event, isolating generation and diagnostic failures.</summary>
        internal string? GetId(ILogThisConfiguration configuration)
        {
            try { return id.Value; }
            catch (Exception exception)
            {
                if (Interlocked.Exchange(ref failureReported, 1) == 0)
                    LogThisRuntimeLogger.ReportFailure(configuration, exception,
                        "LogThis failed while generating a correlation ID.");
                return null;
            }
        }

        #endregion
    }

    #endregion
}
