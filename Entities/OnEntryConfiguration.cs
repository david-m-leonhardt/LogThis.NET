using LogThis.Constants;

namespace LogThis.Entities
{
    internal sealed class OnEntryConfiguration : AccessPointConfigurationBase
    {
        #region Public Properties

        public override string AccessPointMessage { get; set; } = AccessPointConstants.EnteredMessage;

        public override string Message => MessageComponentConstants.OnEntryLabel;

        #endregion
    }
}
