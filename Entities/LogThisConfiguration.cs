using LogThis.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogThis.Entities
{
    public sealed class LogThisConfiguration : ILogThisConfiguration
    {
        #region Public Properties

        public LogLevel OnEntryLogLevel { get; set; }

        public string OnEntryMessage { get; set; }

        public LogLevel OnExceptionLogLevel { get; set; }

        public string OnExceptionMessage { get; set; }

        public LogLevel OnExitLogLevel { get; set; }

        public string OnExitMessage { get; set; }

        public bool UseClassName { get; set; }

        public bool UseMethodName { get; set; }

        #endregion

        #region Constructor

        public LogThisConfiguration()
        {
            OnEntryLogLevel = LogLevel.Information;
            OnEntryMessage = "Entered";
            OnExceptionLogLevel = LogLevel.Warning;
            OnExceptionMessage = "Exception";
            OnExitLogLevel = LogLevel.Information;
            OnExitMessage = "Exited";
            UseClassName = false;
            UseMethodName = true;
        }

        #endregion

    }
}
