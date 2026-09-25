using LogThis.Constants;
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
    private static void Write(
        LogThisRuntime runtime,
        IAccessPointConfiguration accessPoint,
        LogThisMethodContext context,
        object?[]? arguments,
        object? returnValue,
        Exception? exception)
    {
        try
        {
            // Catch construction and provider failures without changing the intercepted method's outcome.
            // Check the level before serializing arguments or the return value.
            if (!accessPoint.LogAccessPoint || !runtime.Logger.IsEnabled(accessPoint.LogLevel)) return;

            ILogThisConfiguration configuration = runtime.Configuration;
            LogParameters parameters = new(configuration.MessageDelimeter);

            string accessPointLabel = ReferenceEquals(accessPoint, configuration.OnEntryConfig)
                ? MessageComponentConstants.OnEntryLabel
                : ReferenceEquals(accessPoint, configuration.OnExceptionConfig)
                    ? MessageComponentConstants.OnExceptionLabel
                    : MessageComponentConstants.OnExitLabel;

            parameters.AddMessage(accessPointLabel);
            parameters.AddArgs(accessPoint.AccessPointMessage);

            if (configuration.LogClassName)
            {
                parameters.AddMessage(MessageComponentConstants.ClassLabel);
                parameters.AddArgs(context.ClassName);
            }

            if (configuration.LogMethodName)
            {
                parameters.AddMessage(MessageComponentConstants.MethodNameLabel);
                parameters.AddArgs(context.MethodName);
            }

            if (arguments != null && configuration.LogMethodArguments)
            {
                parameters.AddMessage(MessageComponentConstants.ArgumentsLabel);
                parameters.AddArgs($"Arguments: {LogThisValueMasker.MaskObjects(arguments!)}");
            }

            if (arguments == null && configuration.LogMethodReturnValue ||
                exception != null && configuration.LogMethodReturnValue)
            {
                parameters.AddMessage(MessageComponentConstants.ReturnValueLabel);
                parameters.AddArgs(exception != null
                    ? "ReturnValue: "
                    : $"ReturnValue: {LogThisValueMasker.MaskObject(returnValue)}");
            }

            parameters.AddMessageComponents(configuration.MessageComponents);
            runtime.Logger.Log(accessPoint.LogLevel, exception, parameters.Message, parameters.Args);
        }
        catch (Exception loggingException)
        {
            if (runtime.Configuration.DebugLogThis)
            {
                // This logger is independent of the application's configured provider and writes only to the console.
                try
                {
                    using ILoggerFactory consoleFactory = LoggerFactory.Create(builder =>
                        builder.SetMinimumLevel(LogLevel.Debug).AddConsole());
                    ILogger consoleLogger = consoleFactory.CreateLogger("LogThis.Diagnostics");
                    consoleLogger.LogDebug(loggingException, "LogThis failed while emitting a method log.");
                }
                catch
                {
                    // Diagnostics must never replace the intercepted method's result or exception.
                }
            }
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
            Write(runtime, runtime.Configuration.OnEntryConfig, context, context.Arguments, null, null);
        }
    }

    /// <summary>Emits an exception event when a scope is active.</summary>
    /// <remarks>If return-value logging is enabled, the exception event currently has an empty return-value component.</remarks>
    /// <param name="context">Method metadata and arguments for this invocation.</param>
    /// <param name="exception">Exception thrown by the method.</param>
    public static void Exception(LogThisMethodContext context, Exception exception)
    {
        LogThisRuntime? runtime = LogThisRuntimeContext.Current;
        if (runtime != null)
        {
            Write(runtime, runtime.Configuration.OnExceptionConfig, context, context.Arguments, null, exception);
        }
    }

    /// <summary>Emits a successful method-exit event when a scope is active.</summary>
    /// <param name="context">Method metadata and arguments for this invocation.</param>
    /// <param name="returnValue">Result produced by the method, if any.</param>
    public static void Exit(LogThisMethodContext context, object? returnValue)
    {
        LogThisRuntime? runtime = LogThisRuntimeContext.Current;
        if (runtime != null)
        {
            Write(runtime, runtime.Configuration.OnExitConfig, context, null, returnValue, null);
        }
    }

    #endregion
}
