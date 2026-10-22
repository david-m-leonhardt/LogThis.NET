using LogThis.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Captures or replaces completion results while retaining the empty exception-result component.</summary>
    internal sealed class CapturedReturnConfigParameter : ConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisReturnValue;

        #endregion

        #region Constructor

        /// <summary>Builds a completion payload without reading the original result when masked.</summary>
        /// <param name="exception">Method failure, or null for a successful completion.</param>
        /// <param name="maskReturn">Whether to substitute a mask instead of capturing the original result.</param>
        /// <param name="returnReplacement">Explicit replacement, or null to use the configured JsonMaskValue.</param>
        /// <param name="returnValue">Original result, inspected only when capture is enabled and no return mask applies.</param>
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public CapturedReturnConfigParameter(Exception? exception, bool maskReturn, string? returnReplacement, object? returnValue,
            LogThisRuntime runtime) :
            base(runtime)
        {
            if (Configuration.LogMethodReturnValue)
            {
                Obj = exception != null
                    ? string.Empty
                    : maskReturn
                        ? new JValue(returnReplacement ?? Configuration.JsonMaskValue).ToString(Formatting.None)
                        : LogThisValueMasker.MaskObject(returnValue);

                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
