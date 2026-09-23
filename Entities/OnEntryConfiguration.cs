using LogThis.Constants;

namespace LogThis.Entities
{
    /// <summary>Defaults for the method-entry event.</summary>
    internal sealed class OnEntryConfiguration : AccessPointConfigurationBase
    {
        #region Public Properties

        /// <summary>Default entry message.</summary>
        /// <value><c>Entered</c> unless overridden.</value>
        public override string AccessPointMessage { get; set; } = AccessPointConstants.EnteredMessage;

        #endregion
    }
}
