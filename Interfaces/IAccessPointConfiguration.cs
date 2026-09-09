using Microsoft.Extensions.Logging;

namespace LogThis.Interfaces
{
    public interface IAccessPointConfiguration
    {
        #region Properties

        string AccessPointMessage { get; set; }

        bool LogAccessPoint { get; set; }

        LogLevel LogLevel { get; set; }

        #endregion
    }
}
