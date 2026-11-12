using LogThis.Constants;
using LogThis.Enum;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities.AccessPoints
{
    internal sealed class OnDemandConfiguration() : AccessPointConfigurationBase
    {
        #region Public Properties

        /// <summary>Default on-demand event text.</summary>
        /// <value><c>OnDemand</c> unless overridden.</value>
        public override string AccessPointMessage { get; set; } = AccessPoint.OnDemandMessage;

        /// <inheritdoc />
        public override AccessPointEventKind EventKind => AccessPointEventKind.OnDemand;

        /// <summary>On-demand events use Information by default.</summary>
        /// <value>Information unless overridden.</value>
        public override LogLevel LogLevel { get; set; } = LogLevel.Information;

        #endregion
    }
}
