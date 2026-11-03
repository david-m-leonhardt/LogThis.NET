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
                Write(RuntimeValuesFactory.BuildOnDemandValues(runtime, memberName, message, values));
            }
            catch (Exception exception)
            {
                ReportFailure(runtime.Configuration, exception, "LogThis failed while emitting an on-demand log.");
            }
        }
    }
}
