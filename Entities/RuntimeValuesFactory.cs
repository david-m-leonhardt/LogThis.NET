using LogThis.Interfaces;

namespace LogThis.Entities
{
    /// <summary>Prepares event-specific invocation data without serializing application values.</summary>
    internal static class RuntimeValuesFactory
    {
        #region Internal Methods

        /// <summary>Prepares entry-event settings and the invocation's included arguments.</summary>
        /// <param name="context">Method identity and captured argument metadata.</param>
        /// <param name="runtime">Active runtime; callers must supply a non-null scope.</param>
        /// <returns>Entry data with no completion result or duration.</returns>
        internal static LogThisRuntimeValues BuildOnEntryValues(LogThisMethodContext context, LogThisRuntime runtime)
        {
            LogThisRuntimeValues runtimeValues = BuildRuntimeValues(runtime.Configuration.OnEntryConfig, context, runtime);
            runtimeValues.Arguments = context.Arguments;

            return runtimeValues;
        }

        /// <summary>Prepares exception-event settings, arguments, and the original method failure.</summary>
        /// <param name="context">Method identity and captured argument metadata.</param>
        /// <param name="durationMs">Previously measured elapsed milliseconds, or null when disabled.</param>
        /// <param name="exception">Original exception to attach to the event.</param>
        /// <param name="runtime">Active runtime; callers must supply a non-null scope.</param>
        /// <returns>Failure data without a fabricated successful result.</returns>
        internal static LogThisRuntimeValues BuildOnExceptionValues(
            LogThisMethodContext context,
            double? durationMs,
            Exception exception,
            LogThisRuntime runtime)
        {
            LogThisRuntimeValues runtimeValues = BuildRuntimeValues(runtime.Configuration.OnExceptionConfig, context, runtime, durationMs);
            runtimeValues.Arguments = context.Arguments;
            runtimeValues.Exception = exception;

            return runtimeValues;
        }

        /// <summary>Prepares a successful exit containing replacement metadata instead of an application result.</summary>
        /// <param name="context">Method identity and captured argument metadata.</param>
        /// <param name="durationMs">Previously measured elapsed milliseconds, or null when disabled.</param>
        /// <param name="replacement">Literal mask, or null to use the selected configuration's JsonMaskValue.</param>
        /// <param name="runtime">Active runtime; callers must supply a non-null scope.</param>
        /// <returns>Exit data that requests masking if return-value capture is enabled.</returns>
        internal static LogThisRuntimeValues BuildOnExitValues(
            LogThisMethodContext context,
            double? durationMs,
            string? replacement,
            LogThisRuntime runtime)
        {
            LogThisRuntimeValues runtimeValues = BuildRuntimeValues(runtime.Configuration.OnExitConfig, context, runtime, durationMs);
            runtimeValues.MaskReturn = true;
            runtimeValues.ReturnReplacement = replacement;

            return runtimeValues;
        }

        /// <summary>Prepares a successful exit with the original result for ordinary configured capture.</summary>
        /// <param name="context">Method identity and captured argument metadata.</param>
        /// <param name="durationMs">Previously measured elapsed milliseconds, or null when disabled.</param>
        /// <param name="returnValue">Original method result, which may be null.</param>
        /// <param name="runtime">Active runtime; callers must supply a non-null scope.</param>
        /// <returns>Exit data without enabling return masking or argument capture.</returns>
        internal static LogThisRuntimeValues BuildOnExitValues(
            LogThisMethodContext context,
            double? durationMs,
            object? returnValue,
            LogThisRuntime runtime)
        {
            LogThisRuntimeValues runtimeValues = BuildRuntimeValues(runtime.Configuration.OnExitConfig, context, runtime, durationMs);
            runtimeValues.ReturnValue = returnValue;

            return runtimeValues;
        }

        #endregion

        #region Private Methods

        /// <summary>Initializes the event fields shared by entry, exit, and exception factories.</summary>
        /// <param name="accessPoint">Selected event configuration.</param>
        /// <param name="context">Invocation metadata used by event components.</param>
        /// <param name="runtime">Active runtime; callers must supply a non-null scope.</param>
        /// <param name="durationMs">Elapsed milliseconds, or null when not supplied.</param>
        /// <returns>Common event data for the calling factory to complete.</returns>
        private static LogThisRuntimeValues BuildRuntimeValues(
            IAccessPointConfiguration accessPoint,
            LogThisMethodContext context,
            LogThisRuntime runtime,
            double? durationMs = null)
        {
            return new LogThisRuntimeValues()
            {
                AccessPoint = accessPoint,
                Context = context,
                DurationMs = durationMs,
                Runtime = runtime
            };
        }

        #endregion
    }
}
