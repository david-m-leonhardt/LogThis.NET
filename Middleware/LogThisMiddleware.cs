using LogThis.Attributes;
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
                LogLevel OnEntryLogLevel = LogLevel.Information,
                string OnEntryMessage = "Entered",
                LogLevel OnExceptionLogLevel = LogLevel.Warning,
                string OnExceptionMessage = "Exception",
                LogLevel OnExitLogLevel = LogLevel.Information,
                string OnExitMessage = "Exited",
                bool UserClassName = false,
                bool UseMethodName = true)
            {
                ILogThisConfiguration logThisConfiguration = new LogThisConfiguration()
                {
                    OnEntryLogLevel = OnEntryLogLevel,
                    OnEntryMessage = OnEntryMessage,
                    OnExceptionLogLevel = OnExceptionLogLevel,
                    OnExceptionMessage = OnExceptionMessage,
                    OnExitLogLevel = OnExitLogLevel,
                    OnExitMessage = OnExitMessage,
                    UseClassName = UserClassName,
                    UseMethodName = UseMethodName
                };

                AddLogThisConfiguration(services, logThisConfiguration);
            }
        }

        extension(WebApplication app)
        {
            public void UseLogThis()
            {
                UseLogThis(app, "LogThis");
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
