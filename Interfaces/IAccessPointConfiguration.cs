using Microsoft.Extensions.Logging;

namespace LogThis.Interfaces
{
    /// <summary>Per-event message, enabled state, and logging level.</summary>
    public interface IAccessPointConfiguration
    {
        #region Properties

        /// <summary>Text emitted for this access point.</summary>
        /// <value>The text passed as the event's structured access-point component.</value>
        string AccessPointMessage { get; set; }

        /// <summary>Whether events at this access point are enabled.</summary>
        /// <value><see langword="true"/> to emit events at this access point.</value>
        bool LogAccessPoint { get; set; }

        /// <summary>Severity of events at this access point.</summary>
        /// <value>The <see cref="LogLevel"/> used for emitted events.</value>
        LogLevel LogLevel { get; set; }

        #endregion
    }
}
