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
        /// <param name="runtimeValues">Completion result or exception, return-mask metadata, and selected configuration.</param>
        public CapturedReturnConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
        {
            if (Configuration.LogMethodReturnValue)
            {
                Obj = runtimeValues.Exception != null
                    ? string.Empty
                    : runtimeValues.MaskReturn
                        ? new JValue(runtimeValues.ReturnReplacement ?? Configuration.JsonMaskValue).ToString(Formatting.None)
                        : LogThisValueMasker.MaskObject(runtimeValues.ReturnValue);

                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
