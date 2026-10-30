using LogThis.Constants;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Obtains the operation chain ID only when correlation is enabled.</summary>
    internal sealed class CorrelationIdConfigParameter : ConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisCorrelationId;

        #endregion

        #region Constructor

        /// <summary>Reuses or lazily generates the enabled chain ID; omits unavailable IDs.</summary>
        /// <param name="runtimeValues">Event data and runtime state containing the shared operation-chain identity.</param>
        public CorrelationIdConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
        {
            if (Configuration.LogCorrelationId && runtimeValues.Runtime.Correlation.GetId(Configuration) is string correlationId)
            {
                Obj = correlationId;
                ShouldAddValue = correlationId != null;
            }
        }

        #endregion
    }
}
