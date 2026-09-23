using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities
{
    /// <summary>Shared defaults for entry, exit, and exception event configuration.</summary>
    internal abstract class AccessPointConfigurationBase : IAccessPointConfiguration
    {
        #region Public Properties

        /// <summary>Text included for this access-point event.</summary>
        /// <value>Empty text until a concrete event supplies its default.</value>
        public virtual string AccessPointMessage { get; set; } = string.Empty;

        /// <summary>Whether this access-point event is emitted.</summary>
        /// <value><see langword="true"/> by default.</value>
        public virtual bool LogAccessPoint { get; set; } = true;

        /// <summary>Severity used for this access-point event.</summary>
        /// <value>Information by default.</value>
        public virtual LogLevel LogLevel { get; set; } = LogLevel.Information;

        #endregion

    }
}
