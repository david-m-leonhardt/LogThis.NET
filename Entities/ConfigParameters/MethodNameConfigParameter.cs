using LogThis.Constants;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Includes the intercepted method name when enabled by the selected configuration.</summary>
    internal sealed class MethodNameConfigParameter : ConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisMethod;

        #endregion

        #region Constructor

        /// <summary>Reads the method name from invocation metadata when enabled.</summary>
        /// <param name="runtimeValues">Invocation metadata and runtime state supplying the method name and inclusion setting.</param>
        public MethodNameConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
        {
            if (Configuration.LogMethodName)
            {
                Obj = runtimeValues.Context.MethodName;
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
