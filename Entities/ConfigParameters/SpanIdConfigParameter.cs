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
        /// <param name="runtimeValues">Event data and selected configuration controlling span enrichment.</param>
        public SpanIdConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
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
