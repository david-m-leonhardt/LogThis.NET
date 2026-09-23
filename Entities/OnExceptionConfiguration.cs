using LogThis.Constants;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities
{
    /// <summary>Defaults for the exception event, including its error level.</summary>
    internal sealed class OnExceptionConfiguration() : AccessPointConfigurationBase
    {
        #region Public Properties

        /// <summary>Exceptions are logged at Error level by default.</summary>
        /// <value>Error unless overridden.</value>
        public override LogLevel LogLevel { get; set; } = LogLevel.Error;

        /// <summary>Default exception message.</summary>
        /// <value><c>Exception</c> unless overridden.</value>
        public override string AccessPointMessage { get; set; } = AccessPointConstants.ExceptionMessage;

        #endregion

    }
}
