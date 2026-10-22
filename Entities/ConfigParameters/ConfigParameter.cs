using LogThis.Interfaces;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Carries a structured placeholder, its value, and whether the component should be emitted.</summary>
    /// <param name="runtime">Selected runtime configuration and operation state.</param>
    internal abstract class ConfigParameter(LogThisRuntime runtime) : IConfigParameter
    {
        #region Protected Properties

        /// <summary>Selected configuration used to decide whether this component is included.</summary>
        protected ILogThisConfiguration Configuration { get; } = runtime.Configuration;

        #endregion

        #region Public Properties

        /// <inheritdoc />
        public abstract string Message { get; }

        /// <inheritdoc />
        public object? Obj { get; protected set; }

        /// <inheritdoc />
        public bool ShouldAddValue { get; protected set; }

        #endregion
    }
}
