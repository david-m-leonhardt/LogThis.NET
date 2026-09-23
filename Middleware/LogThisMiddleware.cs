using LogThis.Constants;
using LogThis.Entities;
using LogThis.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogThis.Middleware
{
    /// <summary>Registers LogThis options and opens runtime scopes for HTTP or manual callers.</summary>
    public static class LogThisMiddleware
    {
        #region Public Methods

        // These service extensions offer default, prebuilt, and parameter-based configuration.
        extension(IServiceCollection services)
        {
            /// <summary>Registers LogThis with default configuration.</summary>
            public void AddLogThisConfiguration()
            {
                AddLogThisConfiguration(services, new LogThisConfiguration());
            }

            /// <summary>Registers one configuration and the scope factory for dependency injection.</summary>
            /// <param name="logThisConfiguration">Configuration instance shared by all registered scopes.</param>
            public void AddLogThisConfiguration(ILogThisConfiguration logThisConfiguration)
            {
                services.AddSingleton(logThisConfiguration);
                services.AddSingleton<ILogThisScopeFactory, LogThisScopeFactory>();
            }

            /// <summary>Registers LogThis using individual options and event-specific settings.</summary>
            /// <param name="DebugLogThis">Report internal logging failures at Debug level.</param>
            /// <param name="JsonFieldsToMask">JSON property names to redact from logged values.</param>
            /// <param name="JsonMaskValue">Replacement text for redacted JSON fields.</param>
            /// <param name="LogClassName">Include the declaring class name in events.</param>
            /// <param name="LogMethodArguments">Include serialized arguments on entry and exception events.</param>
            /// <param name="LogMethodName">Include the method name in events.</param>
            /// <param name="LogMethodReturnValue">Include the serialized result on exit and an empty return-value component on exception events.</param>
            /// <param name="LogOnEntry">Enable method-entry events.</param>
            /// <param name="LogOnException">Enable exception events.</param>
            /// <param name="LogOnExit">Enable successful-exit events.</param>
            /// <param name="MessageComponents">Additional structured properties for every event.</param>
            /// <param name="MessageDelimeter">Separator between message-template components.</param>
            /// <param name="OnEntryLogLevel">Severity of entry events.</param>
            /// <param name="OnEntryMessage">Text emitted for entry events.</param>
            /// <param name="OnExceptionLogLevel">Severity of exception events.</param>
            /// <param name="OnExceptionMessage">Text emitted for exception events.</param>
            /// <param name="OnExitLogLevel">Severity of exit events.</param>
            /// <param name="OnExitMessage">Text emitted for exit events.</param>
            /// <exception cref="ArgumentException">A normalized custom component name is duplicated.</exception>
            public void AddLogThisConfiguration(
                bool DebugLogThis = false,
                List<string>? JsonFieldsToMask = null,
                string JsonMaskValue = MessageComponentConstants.DefaultJsonMaskValue,
                bool LogClassName = false,
                bool LogMethodArguments = false,
                bool LogMethodName = true,
                bool LogMethodReturnValue = false,
                bool LogOnEntry = true,
                bool LogOnException = true,
                bool LogOnExit = true,
                Dictionary<string, object>? MessageComponents = null,
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

                logThisConfiguration.AddMessageComponents(MessageComponents ?? []);

                AddLogThisConfiguration(services, logThisConfiguration);
            }
        }

        // HTTP requests get their own scope; no ambient state is retained between requests.
        extension(WebApplication app)
        {
            /// <summary>Adds HTTP scope middleware using the default logger category.</summary>
            public void UseLogThis()
            {
                UseLogThis(app, MessageComponentConstants.DefaultCategoryName);
            }

            /// <summary>Opens an independent ambient scope around each ASP.NET Core request.</summary>
            /// <param name="categoryName">Logger category assigned to request events.</param>
            /// <exception cref="InvalidOperationException">LogThis services have not been registered.</exception>
            public void UseLogThis(string categoryName)
            {
                ILogThisScopeFactory scopeFactory = app.Services.GetRequiredService<ILogThisScopeFactory>();
                app.Use(async (_, next) =>
                {
                    using IDisposable scope = scopeFactory.BeginScope(categoryName);
                    await next();
                });
            }
        }

        // Non-HTTP hosts establish their scope around the operation they want to log.
        extension(IServiceProvider serviceProvider)
        {
            /// <summary>Opens a scope explicitly for a console or background operation.</summary>
            /// <remarks>Dispose the returned scope after the operation, including any awaited work.</remarks>
            /// <param name="categoryName">Logger category assigned to events in the scope.</param>
            /// <returns>A scope that restores the previous runtime when disposed.</returns>
            /// <exception cref="InvalidOperationException">LogThis services have not been registered.</exception>
            /// <exception cref="ArgumentException"><paramref name="categoryName"/> is empty or whitespace.</exception>
            public IDisposable UseLogThis(string categoryName = MessageComponentConstants.DefaultCategoryName)
            {
                return serviceProvider.GetRequiredService<ILogThisScopeFactory>().BeginScope(categoryName);
            }
        }

        #endregion
    }
}
