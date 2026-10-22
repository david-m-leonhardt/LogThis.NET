using LogThis.Constants;
using System.Diagnostics;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Includes the current W3C span ID when enabled and available.</summary>
    internal sealed class SpanIdConfigParameter : W3CConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisSpanId;

        #endregion

        #region Constructor

        /// <summary>Captures the complete span identifier from the activity observed by this component.</summary>
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public SpanIdConfigParameter(LogThisRuntime runtime) : base(runtime)
        {
            if (Configuration.LogSpanId && 
                activity is { IdFormat: ActivityIdFormat.W3C } current &&
                current.SpanId != default)
            { 
                Obj = current.SpanId.ToHexString();
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
