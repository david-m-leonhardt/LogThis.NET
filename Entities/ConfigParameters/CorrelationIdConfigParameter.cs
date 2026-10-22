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
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public CorrelationIdConfigParameter(LogThisRuntime runtime) : base(runtime)
        {
            if (Configuration.LogCorrelationId && runtime.Correlation.GetId(Configuration) is string correlationId)
            {
                Obj = correlationId;
                ShouldAddValue = correlationId != null;
            }
        }

        #endregion
    }
}
