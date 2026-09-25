using LogThis.Constants;
using LogThis.Entities;
using LogThis.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogThis.Middleware
{
    /// <summary>Registers LogThis options and opens runtime scopes for manual callers.</summary>
    public static class LogThisMiddleware
    {
        #region Private Properties

        private const string sectionKey = "LogThis";

        #endregion

        #region Public Methods

        // Service registration is shared by web, console, and background hosts.
        extension(IServiceCollection services)
        {
            /// <summary>Registers LogThis using defaults, optional DI-provided settings, and optional code overrides.</summary>
            /// <remarks>Register <see cref="IConfiguration"/> before building the service provider to bind its <c>LogThis</c> section. The callback runs after binding when the configuration is first resolved. Unknown keys in a present section cause an error.</remarks>
            /// <param name="configure">Optional changes to make after configuration binding.</param>
            public void AddLogThisConfiguration(Action<LogThisConfiguration>? configure = null)
            {
                RegisterConfiguration(services, provider =>
                {
                    IConfiguration? appConfiguration = provider.GetService<IConfiguration>();
                    LogThisConfiguration options = appConfiguration == null
                        ? new LogThisConfiguration()
                        : BindConfiguration(appConfiguration.GetSection(sectionKey));
                    configure?.Invoke(options);
                    return options;
                });
            }

            /// <summary>Registers LogThis using values bound once from an application configuration section.</summary>
            /// <remarks>Omitted values retain their defaults, but unknown keys cause an error. Configuration reloads do not update the registered singleton.</remarks>
            /// <param name="section">Section containing LogThis option names and values.</param>
            /// <exception cref="ArgumentNullException"><paramref name="section"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentException">Custom message component names become duplicates after normalization.</exception>
            public void AddLogThisConfiguration(IConfigurationSection section)
            {
                ArgumentNullException.ThrowIfNull(section);

                AddLogThisConfiguration(services, BindConfiguration(section));
            }

            /// <summary>Registers one configuration and the scope factory for dependency injection.</summary>
            /// <param name="logThisConfiguration">Configuration instance shared by all registered scopes.</param>
            public void AddLogThisConfiguration(ILogThisConfiguration logThisConfiguration)
            {
                ArgumentNullException.ThrowIfNull(logThisConfiguration);

                RegisterConfiguration(services, _ => logThisConfiguration);
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

        #region Private Methods

        /// <summary>Applies known section values to default options and normalizes custom component names.</summary>
        /// <param name="section">Configuration section to bind once.</param>
        /// <returns>Options with defaults for omitted values.</returns>
        private static LogThisConfiguration BindConfiguration(IConfigurationSection section)
        {
            LogThisConfiguration configuration = new();
            section.Bind(configuration, options => options.ErrorOnUnknownConfiguration = true);
            Dictionary<string, object> components = new(configuration.MessageComponents);
            configuration.MessageComponents.Clear();
            configuration.AddMessageComponents(components);
            return configuration;
        }

        /// <summary>Registers a configuration factory and the scope factory for dependency injection.</summary>
        /// <param name="services">Service collection receiving the registrations.</param>
        /// <param name="factory">Factory that supplies the configuration singleton.</param>
        private static void RegisterConfiguration(
            IServiceCollection services,
            Func<IServiceProvider, ILogThisConfiguration> factory)
        {
            services.AddSingleton(factory);
            services.AddSingleton<ILogThisScopeFactory, LogThisScopeFactory>();
        }

        #endregion
    }
}
