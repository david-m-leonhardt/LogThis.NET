using LogThis.Constants;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Includes the declaring class name when enabled by the selected configuration.</summary>
    internal sealed class ClassNameConfigParameter : ContextMethodConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisClass;

        #endregion

        #region Constructor

        /// <summary>Reads the class name from invocation metadata when enabled.</summary>
        /// <param name="context">Invocation metadata and included arguments.</param>
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public ClassNameConfigParameter(LogThisMethodContext context, LogThisRuntime runtime) : base(context, runtime)
        {
            if (Configuration.LogClassName)
            {
                Obj = context.ClassName;
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
