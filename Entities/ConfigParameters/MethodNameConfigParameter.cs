using LogThis.Constants;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Includes the intercepted method name when enabled by the selected configuration.</summary>
    internal sealed class MethodNameConfigParameter : ContextMethodConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisMethod;

        #endregion

        #region Constructor

        /// <summary>Reads the method name from invocation metadata when enabled.</summary>
        /// <param name="context">Invocation metadata and included arguments.</param>
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public MethodNameConfigParameter(LogThisMethodContext context, LogThisRuntime runtime) : base(context, runtime)
        {
            if (Configuration.LogMethodName)
            {
                Obj = context.MethodName;
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
