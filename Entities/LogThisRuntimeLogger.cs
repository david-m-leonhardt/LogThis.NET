using LogThis.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LogThis.Entities;

/// <summary>
/// Runtime entry points for generated aspect code. An absent ambient scope suppresses logging.
/// </summary>
public static class LogThisRuntimeLogger
{
    #region Private Methods

    /// <summary>Builds and emits one access-point event when its log level is enabled.</summary>
    /// <param name="runtimeValues">Event settings, invocation payload, and selected runtime state.</param>
    private static void Write(LogThisRuntimeValues runtimeValues)
    {
        try
        {
            // Catch construction and provider failures without changing the intercepted method's outcome.
            // Check the level before serializing arguments or the return value.
            if (runtimeValues.ShouldNotLog)
            {
                return;
            }

            LogParameters parameters = new(runtimeValues);

            runtimeValues.Runtime.Logger.Log(
                runtimeValues.AccessPoint.LogLevel,
                runtimeValues.Exception,
                parameters.Message,
                parameters.Args);
        }
        catch (Exception loggingException)
        {
            ReportFailure(runtimeValues.Runtime.Configuration, loggingException, "LogThis failed while emitting a method log.");
        }
    }

    #endregion

    #region Internal Methods

    /// <summary>Reports logging failures through the existing independent console diagnostics.</summary>
    /// <param name="configuration">Controls debug diagnostics.</param>
    /// <param name="exception">Failure being reported.</param>
    /// <param name="message">Diagnostic message.</param>
    internal static void ReportFailure(ILogThisConfiguration configuration, Exception exception, string message)
    {
        try
        {
            if (configuration.DebugLogThis)
            {
                // This logger is independent of the application's configured provider and writes only to the console.
                using ILoggerFactory consoleFactory = LoggerFactory.Create(builder =>
                    builder.SetMinimumLevel(LogLevel.Debug).AddConsole());
                ILogger consoleLogger = consoleFactory.CreateLogger("LogThis.Diagnostics");
                consoleLogger.LogDebug(exception, message);
            }
        }
        catch
        {
            // Diagnostics must never replace application results or prevent scope creation.
        }
    }

    #endregion

    #region Public Methods

    /// <summary>Emits a method-entry event when a scope is active.</summary>
    /// <param name="context">Method metadata and arguments for this invocation.</param>
    public static void Entry(LogThisMethodContext context)
    {
        LogThisRuntime? runtime = LogThisRuntimeContext.Current;
        if (runtime != null)
        {
            Write(RuntimeValuesFactory.BuildOnEntryValues(context, runtime));
        }
    }

    /// <summary>Emits an exception event when a scope is active.</summary>
    /// <remarks>If return-value logging is enabled, the exception event currently has an empty return-value component.</remarks>
    /// <param name="context">Method metadata and arguments for this invocation.</param>
    /// <param name="exception">Exception thrown by the method.</param>
    public static void Exception(LogThisMethodContext context, Exception exception)
        => Exception(context, exception, null);

    /// <summary>Emits an exception event with optional elapsed milliseconds.</summary>
    /// <param name="context">Method metadata and arguments.</param>
    /// <param name="exception">Original method failure.</param>
    /// <param name="durationMs">Elapsed milliseconds, or null when disabled.</param>
    public static void Exception(LogThisMethodContext context, Exception exception, double? durationMs)
    {
        LogThisRuntime? runtime = LogThisRuntimeContext.Current;
        if (runtime != null)
        {
            Write(RuntimeValuesFactory.BuildOnExceptionValues(context, durationMs, exception, runtime));
        }
    }

    /// <summary>Emits a successful exit event with optional elapsed milliseconds.</summary>
    /// <param name="context">Method metadata and arguments.</param>
    /// <param name="returnValue">Original method result.</param>
    /// <param name="durationMs">Elapsed milliseconds, or null when disabled.</param>
    public static void Exit(LogThisMethodContext context, object? returnValue, double? durationMs)
    {
        LogThisRuntime? runtime = LogThisRuntimeContext.Current;
        if (runtime != null)
        {
            Write(RuntimeValuesFactory.BuildOnExitValues(context, durationMs, returnValue, runtime));
        }
    }

    /// <summary>Emits a successful exit with a replacement result, without receiving the application result.</summary>
    /// <param name="context">Method metadata and arguments.</param>
    /// <param name="replacement">Literal replacement, or null to use the selected profile's JsonMaskValue.</param>
    /// <param name="durationMs">Elapsed milliseconds, or null when disabled.</param>
    /// <remarks>Event, level, and return-capture settings still apply. Intended for generated aspect code.</remarks>
    public static void ExitMasked(LogThisMethodContext context, string? replacement, double? durationMs)
    {
        LogThisRuntime? runtime = LogThisRuntimeContext.Current;
        if (runtime != null)
        {
            Write(RuntimeValuesFactory.BuildOnExitValues(context, durationMs, replacement, runtime));
        }
    }

    /// <summary>Stops timing before completion-event formatting begins.</summary>
    /// <param name="startTimestamp">Timestamp captured immediately before method execution.</param>
    /// <returns>Elapsed milliseconds, or null without reading the clock when disabled.</returns>
    public static double? FinishDuration(long? startTimestamp) => startTimestamp.HasValue
        ? Stopwatch.GetElapsedTime(startTimestamp.Value).TotalMilliseconds
        : null;

    /// <summary>Selects a method's complete configuration for logging and serialization, restoring its caller on disposal.</summary>
    /// <param name="profile">Named profile, or null to select the operation's default configuration.</param>
    /// <returns>A selection scope, or null outside an operation scope.</returns>
    /// <exception cref="ArgumentException">The profile name is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The named profile is not registered in the active operation.</exception>
    /// <remarks>Configuration errors are raised before application execution. This does not generate or replace correlation IDs.</remarks>
    public static IDisposable? SelectProfile(string? profile)
    {
        LogThisRuntime? runtime = LogThisRuntimeContext.Current;

        if (runtime == null)
        {
            return null;
        }

        LogThisProfiles.Selection selection = runtime.Profiles.Select(profile);

        return LogThisRuntimeContext.Push(runtime with
        {
            Configuration = selection.Configuration,
            MessageComponents = selection.MessageComponents
        });
    }

    /// <summary>Starts elapsed timing only when enabled in the active scope.</summary>
    /// <returns>A Stopwatch timestamp, or null when timing is disabled or configuration fails.</returns>
    public static long? StartDuration()
    {
        try
        {
            return LogThisRuntimeContext.Current?.Configuration.LogMethodDuration == true
                ? Stopwatch.GetTimestamp()
                : null;
        }
        catch
        {
            // Optional logging must not prevent application execution.
            return null;
        }
    }

    #endregion
}
