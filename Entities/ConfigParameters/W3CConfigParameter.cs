using System.Diagnostics;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Reads ambient activity context for a trace or span component without creating an activity.</summary>
    internal abstract class W3CConfigParameter : ConfigParameter
    {
        #region Internal Properties

        /// <summary>Ambient activity observed when this component is constructed, if enrichment is enabled.</summary>
        internal Activity? activity;

        #endregion

        #region Constructor

        /// <summary>Reads Activity.Current only when trace or span enrichment is enabled.</summary>
        /// <param name="runtimeValues">Event data and selected configuration controlling ambient activity reads.</param>
        protected W3CConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
        {
            // Read ambient context for this component rather than caching it at operation-scope creation.
            activity = Configuration.LogTraceId || Configuration.LogSpanId ? Activity.Current : null;
        }

        #endregion
    }
}
