using LogThis.Interfaces;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Carries a structured placeholder, its value, and whether the component should be emitted.</summary>
    /// <param name="runtimeValues">Event data and runtime state supplying the selected configuration.</param>
    internal abstract class ConfigParameter(LogThisRuntimeValues runtimeValues) : IConfigParameter
    {
        #region Protected Properties

        /// <summary>Selected configuration used to decide whether this component is included.</summary>
        protected ILogThisConfiguration Configuration { get; } = runtimeValues.Runtime.Configuration;

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
