using LogThis.Constants;
using System.Diagnostics;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Includes the current W3C trace ID when enabled and available.</summary>
    internal sealed class TraceIdConfigParameter : W3CConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisTraceId;

        #endregion

        #region Constructor

        /// <summary>Captures the complete trace identifier from the activity observed by this component.</summary>
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public TraceIdConfigParameter(LogThisRuntime runtime) : base(runtime)
        {
            if (Configuration.LogTraceId && 
                activity is { IdFormat: ActivityIdFormat.W3C } current &&
                current.TraceId != default)
            {
                Obj = current.TraceId.ToHexString();
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
