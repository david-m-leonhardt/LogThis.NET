using LogThis.Constants;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Captures named arguments only when an argument payload is present and capture is enabled.</summary>
    internal sealed class CapturedArgumentsConfigParameter : ConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisArguments;

        #endregion

        #region Constructor

        /// <summary>Serializes eligible arguments using the active exclusions and masking rules.</summary>
        /// <param name="runtimeValues">Argument payload, invocation metadata, and selected capture configuration.</param>
        public CapturedArgumentsConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
        {
            if (runtimeValues.Arguments != null && Configuration.LogMethodArguments)
            {
                Obj = LogThisValueMasker.FormatCapture(LogThisValueMasker.MaskArguments(runtimeValues.Context), Configuration);
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
