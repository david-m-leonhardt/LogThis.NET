using LogThis.Interfaces;
using MethodBoundaryAspect.Fody.Attributes;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities
{
    internal abstract class AccessPointConfigurationBase : MessageComponentBase, IAccessPointConfiguration
    {
        #region Public Properties

        public virtual string AccessPointMessage { get; set; } = string.Empty;

        public virtual bool LogAccessPoint { get; set; } = true;

        public virtual LogLevel LogLevel { get; set; } = LogLevel.Information;

        #endregion

        #region Public Methods

        public override object BuildArg(MethodExecutionArgs arg)
        {
            return AccessPointMessage;
        }

        public override bool IncludeThisBuilder(IAccessPointConfiguration accessPointConfig)
        {
            return accessPointConfig.GetType() == GetType();
        }

        #endregion
    }
}
