using LogThis.Entities;
using System.Runtime.CompilerServices;
using static LogThis.Utility.LogThisRuntimeLogger;

namespace LogThis.Utility
{
    /// <summary>Emits explicit checkpoints through the active LogThis scope.</summary>
    public static class LogOnDemand
    {
        /// <summary>Emits one event with an optional literal message and supplied values; does nothing outside a scope.</summary>
        /// <param name="message">Optional literal message.</param>
        /// <param name="profile">Exact profile name, or null/empty for the registered default.</param>
        /// <param name="values">Values to capture using existing serialization safeguards.</param>
        /// <param name="memberName">Caller member supplied by the compiler.</param>
        /// <exception cref="InvalidOperationException">The selected profile is not registered.</exception>
        public static void LogThisNow(
            string? message = null,
            string? profile = null,
            object?[]? values = null,
            [CallerMemberName] string memberName = "")
            => LogNow(message, profile, values, memberName, isException: false, exception: null);

        /// <summary>Emits an explicit exception event, passing the original exception to the provider when supplied.</summary>
        /// <param name="exception">Exception to attach, or null to emit without an attached exception.</param>
        /// <param name="message">Optional literal message.</param>
        /// <param name="profile">Exact profile name, or null/empty for the registered default.</param>
        /// <param name="values">Values to capture using existing serialization safeguards.</param>
        /// <param name="memberName">Caller member supplied by the compiler.</param>
        /// <exception cref="InvalidOperationException">The selected profile is not registered.</exception>
        public static void LogThisExceptionNow(
            Exception? exception,
            string? message = null,
            string? profile = null,
            object?[]? values = null,
            [CallerMemberName] string memberName = "")
            => LogNow(message, profile, values, memberName, isException: true, exception);

        private static void LogNow(string? message, string? profile, object?[]? values, string memberName,
            bool isException, Exception? exception)
        {
            if (LogThisRuntimeContext.Current == null)
            {
                return;
            }
            
            // Profile selection errors deliberately propagate.
            using IDisposable? selection = SelectProfile(profile);
            LogThisRuntime runtime = LogThisRuntimeContext.Current!;
            
            try
            {
                Write(RuntimeValuesFactory.BuildOnDemandValues(runtime, memberName, message, values, isException, exception));
            }
            catch (Exception loggingException)
            {
                ReportFailure(runtime.Configuration, loggingException, "LogThis failed while emitting an on-demand log.");
            }
        }
    }
}
