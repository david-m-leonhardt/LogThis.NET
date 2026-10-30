using LogThis.Constants;
using LogThis.Enum;

namespace LogThis.Entities
{
    /// <summary>Defaults for the method-entry event.</summary>
    internal sealed class OnEntryConfiguration : AccessPointConfigurationBase
    {
        #region Public Properties

        /// <summary>Default entry message.</summary>
        /// <value><c>Entered</c> unless overridden.</value>
        public override string AccessPointMessage { get; set; } = AccessPoint.EnteredMessage;

        /// <inheritdoc />
        public override AccessPointEventKind EventKind => AccessPointEventKind.Entry;

        #endregion
    }
}
