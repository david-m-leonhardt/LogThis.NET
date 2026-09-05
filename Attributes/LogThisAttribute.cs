using LogThis.Interfaces;
using MethodBoundaryAspect.Fody.Attributes;
using Microsoft.Extensions.Logging;

namespace LogThis.Attributes
{
    [AttributeUsage(validOn: AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class LogThisAttribute : OnMethodBoundaryAspect
    {

        #region Private Properties

        private static ILogger? _logger;

        private static ILogThisConfiguration? config;

        #endregion

        #region Private Methods

        private object[] BuildArgs(MethodExecutionArgs arg)
        {
            List<object> args = [];

            if (config.UseClassName)
            {
                args.Add(arg.Method.DeclaringType.Name);
            }
            if (config.UseMethodName)
            {
                args.Add(arg.Method.Name);
            }

            return [.. args];
        }

        private string BuildMessage(string interceptMessage)
        {
            List<string> messageComponents = [];

            messageComponents.Add(interceptMessage);

            if (config.UseClassName)
            {
                messageComponents.Add("{Class}");
            }
            if (config.UseMethodName)
            {
                messageComponents.Add("{Name}");
            }

            return string.Join(" | ", messageComponents);
        }

        private void LogMessage(LogLevel logLevel, string interceptMessage, MethodExecutionArgs arg)
        {
            if (logLevel != LogLevel.None && _logger.IsEnabled(logLevel))
            {
                string message = BuildMessage(interceptMessage);
                object[] args = BuildArgs(arg);

                _logger?.Log(logLevel, message, args);
            }
        }

        #endregion

        #region Public Methods  

        public static void Initialize(ILogger? logger, ILogThisConfiguration? logThisConfiguration)
        {
            _logger = logger;
            config = logThisConfiguration;
        }

        public override void OnEntry(MethodExecutionArgs arg)
        {

            LogMessage(config.OnEntryLogLevel, config.OnEntryMessage, arg);
        }

        public override void OnException(MethodExecutionArgs arg)
        {
            _logger?.Log(config.OnExceptionLogLevel, arg.Exception, config.OnExceptionMessage, arg.Method.Name);
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            LogMessage(config.OnExitLogLevel, config.OnExitMessage, arg);
        }

        #endregion
    }
}
  