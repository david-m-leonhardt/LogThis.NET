using LogThis.Constants;

namespace LogThis.Entities
{
    internal sealed class OnExitConfiguration() : AccessPointConfigurationBase
    {
        #region Public Properties

        public override string AccessPointMessage { get; set; } = AccessPointConstants.ExitMessage;

        public override string Message => MessageComponentConstants.OnExitLabel;

        #endregion
    }
}
