using LogThis.Attributes;
using LogThis.Constants;
using LogThis.Entities;
using LogThis.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogThis.Middleware
{
    public static class LogThisMiddleware
    {
        #region Public Methods

        extension(IServiceCollection services)
        {
            public void AddLogThisConfiguration()
            {
                AddLogThisConfiguration(services, new LogThisConfiguration());
            }

            public void AddLogThisConfiguration(ILogThisConfiguration logThisConfiguration)
            {
                services.AddSingleton(logThisConfiguration);
            }

            public void AddLogThisConfiguration(
                bool DebugLogThis = false,
                List<string> JsonFieldsToMask = null,
                string JsonMaskValue = MessageComponentConstants.DefaultJsonMaskValue,
                bool LogClassName = false,
                bool LogMethodArguments = false,
                bool LogMethodName = true,
                bool LogMethodReturnValue = false,
                bool LogOnEntry = true,
                bool LogOnException = true,
                bool LogOnExit = true,
                Dictionary<string, object> MessageComponents = null,
                string MessageDelimeter = MessageComponentConstants.DefaultDelimeter,
                LogLevel OnEntryLogLevel = LogLevel.Information,
                string OnEntryMessage = AccessPointConstants.EnteredMessage,
                LogLevel OnExceptionLogLevel = LogLevel.Error,
                string OnExceptionMessage = AccessPointConstants.ExceptionMessage,
                LogLevel OnExitLogLevel = LogLevel.Information,
                string OnExitMessage = AccessPointConstants.ExitMessage
                )
            {
                LogThisConfiguration logThisConfiguration = new()
                {
                    DebugLogThis = DebugLogThis,
                    JsonFieldsToMask = JsonFieldsToMask ?? [],
                    JsonMaskValue = JsonMaskValue,
                    LogClassName = LogClassName,
                    LogMethodArguments = LogMethodArguments,
                    LogMethodName = LogMethodName,
                    LogMethodReturnValue = LogMethodReturnValue,
                    MessageDelimeter = MessageDelimeter,
                    OnEntryConfig = new OnEntryConfiguration
                    {
                        AccessPointMessage = OnEntryMessage,
                        LogAccessPoint = LogOnEntry,
                        LogLevel = OnEntryLogLevel
                    },
                    OnExceptionConfig = new OnExceptionConfiguration
                    {
                        AccessPointMessage = OnExceptionMessage,
                        LogAccessPoint = LogOnException,
                        LogLevel = OnExceptionLogLevel
                    },
                    OnExitConfig = new OnExitConfiguration
                    {
                        AccessPointMessage = OnExitMessage,
                        LogAccessPoint = LogOnExit,
                        LogLevel = OnExitLogLevel
                    }
                };

                logThisConfiguration.AddMessageComponents(MessageComponents);

                AddLogThisConfiguration(services, logThisConfiguration);
            }
        }

        extension(WebApplication app)
        {
            public void UseLogThis()
            {
                UseLogThis(app, MessageComponentConstants.DefaultCategoryName);
            }

            public void UseLogThis(string categoryName)
            {
                ILoggerFactory? loggerFactory = app.Services.GetService<ILoggerFactory>();
                ILogger? logger = loggerFactory?.CreateLogger(categoryName);

                ILogThisConfiguration? loggerConfiguration = app.Services.GetService<ILogThisConfiguration>();

                LogThisAttribute.Initialize(logger, loggerConfiguration);
            }
        }

        #endregion
    }
}
