using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities;

/// <summary>The logger and configuration selected for one active logging scope.</summary>
/// <param name="Logger">Logger created for the scope's category.</param>
/// <param name="Configuration">Options shared by events in the scope.</param>
internal sealed record LogThisRuntime(
    ILogger Logger,
    ILogThisConfiguration Configuration);

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
}
