using LogThis.Constants;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Includes the declaring class name when enabled by the selected configuration.</summary>
    internal sealed class ClassNameConfigParameter : ConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisClass;

        #endregion

        #region Constructor

        /// <summary>Reads the class name from invocation metadata when enabled.</summary>
        /// <param name="runtimeValues">Selected runtime configuration and operation state.</param>
        public ClassNameConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
        {
            if (Configuration.LogClassName)
            {
                Obj = runtimeValues.Context.ClassName;
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
