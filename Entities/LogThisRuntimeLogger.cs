using System.Diagnostics;
using LogThis.Constants;
using LogThis.Entities.ConfigParameters;
using LogThis.Enum;
using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities;

/// <summary>
/// Runtime entry points for generated aspect code. An absent ambient scope suppresses logging.
/// </summary>
public static class LogThisRuntimeLogger
{
    #region Private Methods

    /// <summary>Builds and emits one access-point event when its log level is enabled.</summary>
    /// <param name="runtime">Logger and configuration for the active scope.</param>
    /// <param name="accessPoint">Settings for the event being emitted.</param>
    /// <param name="context">Method metadata and arguments for this invocation.</param>
    /// <param name="arguments">Arguments to log, or <see langword="null"/> for exit.</param>
    /// <param name="returnValue">Result to log on exit, when configured.</param>
    /// <param name="exception">Exception associated with the event, if any.</param>
    /// <param name="durationMs">Elapsed milliseconds, or null when timing is disabled.</param>
    /// <param name="maskReturn">Whether the successful result is replaced without capture.</param>
    /// <param name="returnReplacement">Literal replacement, or null for the configured mask.</param>
    private static void Write(
        LogThisRuntime runtime,
        IAccessPointConfiguration accessPoint,
        LogThisMethodContext context,
        object?[]? arguments,
        object? returnValue,
        Exception? exception,
        double? durationMs = null,
        bool maskReturn = false,
        string? returnReplacement = null)
    {
        try
        {
            // Catch construction and provider failures without changing the intercepted method's outcome.
            // Check the level before serializing arguments or the return value.
            if (!accessPoint.LogAccessPoint || !runtime.Logger.IsEnabled(accessPoint.LogLevel))
            {
                return;
            }

            LogParameters parameters = new(runtime);

            List<IConfigParameter> configParameters = 
                BuildConfigParameters(accessPoint, arguments, context, durationMs, exception, maskReturn, returnReplacement, returnValue, runtime);

            configParameters.ForEach(parameters.Add);

            parameters.AddMessageComponents(runtime.MessageComponents);
            runtime.Logger.Log(accessPoint.LogLevel, exception, parameters.Message, parameters.Args);
        }
        catch (Exception loggingException)
        {
            ReportFailure(runtime.Configuration, loggingException, "LogThis failed while emitting a method log.");
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

    #region Private Methods

    /// <summary>Captures payloads and constructs event components in their rendered order.</summary>
    /// <remarks>Called after event and level checks. Payload capture precedes lazy correlation-ID generation.</remarks>
    private static List<IConfigParameter> BuildConfigParameters(
        IAccessPointConfiguration accessPoint,
        object?[]? arguments,
        LogThisMethodContext context,
        double? durationMs,
        Exception? exception,
        bool maskReturn,
        string? returnReplacement,
        object? returnValue,
        LogThisRuntime runtime)
    {
        CapturedArgumentsConfigParameter capturedArguments = new(arguments, context, runtime);

        CapturedReturnConfigParameter? capturedReturn =
            accessPoint.EventKind == AccessPointEventKind.Entry
                ? null
                : new CapturedReturnConfigParameter(exception, maskReturn, returnReplacement, returnValue, runtime);

        List<IConfigParameter> configParams =
        [
            new AccessPointConfigParameter(accessPoint, runtime),
            new CorrelationIdConfigParameter(runtime),
            new TraceIdConfigParameter(runtime),
            new SpanIdConfigParameter(runtime),
            new DurationConfigParameter(durationMs, runtime),
            new ClassNameConfigParameter(context, runtime),
            new MethodNameConfigParameter(context, runtime),
            capturedArguments
        ];

        if (capturedReturn != null)
        {
            configParams.Add(capturedReturn);
        }

        return configParams;
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
            Write(runtime, runtime.Configuration.OnEntryConfig, context, context.Arguments, null, null);
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
            Write(runtime, runtime.Configuration.OnExceptionConfig, context, context.Arguments, null, exception, durationMs);
        }
    }

    /// <summary>Emits a successful method-exit event when a scope is active.</summary>
    /// <param name="context">Method metadata and arguments for this invocation.</param>
    /// <param name="returnValue">Result produced by the method, if any.</param>
    public static void Exit(LogThisMethodContext context, object? returnValue)
        => Exit(context, returnValue, null);

    /// <summary>Emits a successful exit event with optional elapsed milliseconds.</summary>
    /// <param name="context">Method metadata and arguments.</param>
    /// <param name="returnValue">Original method result.</param>
    /// <param name="durationMs">Elapsed milliseconds, or null when disabled.</param>
    public static void Exit(LogThisMethodContext context, object? returnValue, double? durationMs)
    {
        LogThisRuntime? runtime = LogThisRuntimeContext.Current;
        if (runtime != null)
        {
            Write(runtime, runtime.Configuration.OnExitConfig, context, null, returnValue, null, durationMs);
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
            Write(runtime, runtime.Configuration.OnExitConfig, context, null, null, null, durationMs, true, replacement);
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
