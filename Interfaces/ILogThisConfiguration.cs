using Microsoft.Extensions.Logging;

namespace LogThis.Interfaces
{
    public interface ILogThisConfiguration
    {
        #region Properties

        LogLevel OnEntryLogLevel { get; set; }

        string OnEntryMessage { get; set; }

        LogLevel OnExceptionLogLevel { get; set; }

        string OnExceptionMessage { get; set; }

        LogLevel OnExitLogLevel { get; set; }

        string OnExitMessage { get; set; }

        bool UseClassName { get; set; }

        bool UseMethodName {  get; set; }

        #endregion
    }
}
