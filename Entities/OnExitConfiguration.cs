using LogThis.Constants;

namespace LogThis.Entities
{
    /// <summary>Defaults for the successful method-exit event.</summary>
    internal sealed class OnExitConfiguration() : AccessPointConfigurationBase
    {
        #region Public Properties

        /// <summary>Default successful-exit message.</summary>
        /// <value><c>Exited</c> unless overridden.</value>
        public override string AccessPointMessage { get; set; } = AccessPointConstants.ExitMessage;

        #endregion
    }
}
