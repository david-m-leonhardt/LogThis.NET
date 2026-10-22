namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Shares invocation metadata with components that describe the intercepted method.</summary>
    /// <param name="context">Invocation metadata and included arguments.</param>
    /// <param name="runtime">Selected runtime configuration and operation state.</param>
    internal abstract class ContextMethodConfigParameter(LogThisMethodContext context, LogThisRuntime runtime) : ConfigParameter(runtime)
    {
        #region Internal Properties

        /// <summary>Invocation metadata used by class-name and method-name components.</summary>
        internal LogThisMethodContext context = context;

        #endregion
    }
}
