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
        /// <param name="arguments">Argument payload marker, or null when arguments are not part of this event.</param>
        /// <param name="context">Invocation metadata and included arguments.</param>
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public CapturedArgumentsConfigParameter(object?[]? arguments, LogThisMethodContext context, LogThisRuntime runtime) : 
            base(runtime)
        {
            if (arguments != null && Configuration.LogMethodArguments)
            {
                Obj = LogThisValueMasker.MaskArguments(context);
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
