using LogThis.Constants;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities
{
    internal sealed class OnExceptionConfiguration() : AccessPointConfigurationBase
    {
        #region Public Properties

        public override LogLevel LogLevel { get; set; } = LogLevel.Error;

        public override string AccessPointMessage { get; set; } = AccessPointConstants.ExceptionMessage;

        public override string Message => MessageComponentConstants.OnExceptionLabel;

        #endregion

    }
}
