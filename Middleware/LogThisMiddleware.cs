using LogThis.Constants;
using LogThis.Entities;
using LogThis.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogThis.Middleware
{
    /// <summary>Registers LogThis options and opens runtime scopes for manual callers.</summary>
    public static class LogThisMiddleware
    {
        #region Private Properties

    /// <summary>Root configuration section used for default settings and named profiles.</summary>
    private const string sectionKey = "LogThis";

        #endregion

        #region Public Methods

        // Service registration is shared by web, console, and background hosts.
        extension(IServiceCollection services)
        {
            /// <summary>Registers LogThis defaults and named profiles using optional DI-provided settings and default-configuration overrides.</summary>
            /// <remarks>Register <see cref="IConfiguration"/> before building the service provider to bind its <c>LogThis</c> section. The callback runs after default binding when configuration is first resolved. Profiles are independent library-default configurations validated when the scope factory is resolved. Unknown keys cause an error.</remarks>
            /// <param name="configure">Optional changes to make after configuration binding.</param>
            /// <exception cref="ArgumentException">A custom component has a reserved or malformed name when configuration is resolved.</exception>
            public void AddLogThisConfiguration(Action<LogThisConfiguration>? configure = null)
            {
                RegisterConfiguration(services, provider =>
                {
                    IConfiguration? appConfiguration = provider.GetService<IConfiguration>();

                    LogThisConfiguration options = appConfiguration == null
                        ? new LogThisConfiguration()
                        : BindDefaultConfiguration(appConfiguration.GetSection(sectionKey));

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

                LogThisConfiguration configuration = BindDefaultConfiguration(section);

                RegisterConfiguration(services, _ => configuration, _ => section);
            }

            /// <summary>Registers one configuration and the scope factory for dependency injection.</summary>
            /// <param name="logThisConfiguration">Configuration instance shared by all registered scopes.</param>
            /// <remarks>Custom names are validated on first resolution and copied when the scope factory is created; later dictionary edits do not change its events.</remarks>
            /// <exception cref="ArgumentException">A custom component has a reserved or malformed name when configuration is resolved.</exception>
            public void AddLogThisConfiguration(ILogThisConfiguration logThisConfiguration)
            {
                ArgumentNullException.ThrowIfNull(logThisConfiguration);

                RegisterConfiguration(services, _ => logThisConfiguration);
            }

            /// <summary>Registers a named profile callback applied after its JSON values, starting from library defaults.</summary>
            /// <param name="name">Nonempty name, trimmed and matched case-insensitively.</param>
            /// <param name="configure">Changes to only this profile.</param>
            /// <exception cref="ArgumentException">The name is empty or already defined in code.</exception>
            /// <remarks>Matching JSON is intentional layering. Profiles do not inherit the default configuration. Validation occurs when the scope factory is first resolved.</remarks>
            public void AddLogThisProfile(string name, Action<LogThisConfiguration> configure)
            {
                ArgumentNullException.ThrowIfNull(configure);

                RegisterProfile(services, new ProfileDefinition(LogThisProfiles.NormalizeName(name), configure, null));
            }

            /// <summary>Registers a complete named configuration, replacing any JSON definition for that name.</summary>
            /// <param name="name">Nonempty name, trimmed and matched case-insensitively.</param>
            /// <param name="configuration">Complete configuration, not an overlay on default or JSON settings.</param>
            /// <exception cref="ArgumentException">The name is empty or already defined in code.</exception>
            /// <remarks>Custom fields are validated and copied when the scope factory is first resolved.</remarks>
            public void AddLogThisProfile(string name, LogThisConfiguration configuration)
            {
                ArgumentNullException.ThrowIfNull(configuration);

                RegisterProfile(services, new ProfileDefinition(LogThisProfiles.NormalizeName(name), null, configuration));
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
            public IDisposable UseLogThis(string categoryName = MessageComponents.DefaultCategoryName)
            {
                return serviceProvider.GetRequiredService<ILogThisScopeFactory>().BeginScope(categoryName);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>Applies known section values to default options and normalizes custom component names.</summary>
        /// <param name="section">Configuration section to bind once.</param>
        /// <returns>Options with defaults for omitted values.</returns>
        private static LogThisConfiguration BindConfiguration(IConfiguration section)
        {
            LogThisConfiguration configuration = new();

            section.Bind(configuration, options => options.ErrorOnUnknownConfiguration = true);

            Dictionary<string, object> components = new(configuration.MessageComponents);

            configuration.MessageComponents.Clear();

            configuration.AddMessageComponents(components);
            
            return configuration;
        }

        /// <summary>Binds default settings while excluding the one permitted profiles container.</summary>
        private static LogThisConfiguration BindDefaultConfiguration(IConfigurationSection section)
        {
            IEnumerable<KeyValuePair<string, string?>> values = section.AsEnumerable(makePathsRelative: true).Where(entry =>
                !entry.Key.Equals("Profiles", StringComparison.OrdinalIgnoreCase) &&
                !entry.Key.StartsWith("Profiles:", StringComparison.OrdinalIgnoreCase));

            return BindConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        }

        /// <summary>Adds exactly one code definition per normalized name, retaining default registration.</summary>
        private static void RegisterProfile(IServiceCollection services, ProfileDefinition definition)
        {
            if (services.Any(descriptor => descriptor.ImplementationInstance is ProfileDefinition existing &&
                existing.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"LogThis profile '{definition.Name}' is already registered in code. Use one definition per name.");
            }

            if (!services.Any(descriptor => descriptor.ServiceType == typeof(ILogThisConfiguration)))
            {
                services.AddLogThisConfiguration();
            }

            services.AddSingleton(definition);
        }

        /// <summary>Builds independent profile configurations, then snapshots and validates all custom fields.</summary>
        private static LogThisProfiles BuildProfiles(IServiceProvider provider, IConfigurationSection? section)
        {
            Dictionary<string, LogThisConfiguration> named = new(StringComparer.OrdinalIgnoreCase);

            IConfigurationSection? container = section?.GetSection("Profiles");

            if (container?.Value != null)
            {
                throw new ArgumentException("LogThis:Profiles must be a container of named configurations.");
            }

            foreach (IConfigurationSection child in container?.GetChildren() ?? [])
            {
                string name = LogThisProfiles.NormalizeName(child.Key);

                if (child.Value != null)
                {
                    throw new ArgumentException($"LogThis profile '{name}' must contain configuration settings.");
                }

                if (!named.TryAdd(name, BindConfiguration(child)))
                {
                    throw new ArgumentException($"LogThis profile '{name}' has duplicate JSON definitions after name normalization.");
                }
            }
            foreach (ProfileDefinition definition in provider.GetServices<ProfileDefinition>())
            {
                if (definition.Configuration != null)
                {
                    named[definition.Name] = definition.Configuration;
                    continue;
                }

                if (!named.TryGetValue(definition.Name, out LogThisConfiguration? options))
                {
                    options = new();
                }

                definition.Configure!(options);

                named[definition.Name] = options;
            }
            return new LogThisProfiles(provider.GetRequiredService<ILogThisConfiguration>(),
                named.ToDictionary(entry => entry.Key, entry => (ILogThisConfiguration)entry.Value, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>Registers a configuration factory and the scope factory for dependency injection.</summary>
        /// <param name="services">Service collection receiving the registrations.</param>
        /// <param name="factory">Factory that supplies the configuration singleton.</param>
        /// <param name="profileSection">Optional explicit section for named configurations.</param>
        private static void RegisterConfiguration(
            IServiceCollection services,
            Func<IServiceProvider, ILogThisConfiguration> factory,
            Func<IServiceProvider, IConfigurationSection?>? profileSection = null)
        {
            services.AddSingleton(provider =>
            {
                ILogThisConfiguration configuration = factory(provider);
                // Validate every registration path when options are first resolved.
                LogThisProfiles.ValidateCaptureLength(configuration);
                LogParameters.PrepareComponents(configuration.MessageComponents);
                return configuration;
            });

            services.AddSingleton<LogThisProfiles>(provider => BuildProfiles(provider,
                profileSection != null ? profileSection(provider) : provider.GetService<IConfiguration>()?.GetSection(sectionKey)));

            services.AddSingleton<ILogThisScopeFactory>(provider => new LogThisScopeFactory(
                provider.GetRequiredService<ILoggerFactory>(),
                provider.GetRequiredService<ILogThisConfiguration>(),
                provider.GetRequiredService<LogThisProfiles>()));
        }

        #endregion

        #region Private Type

        /// <summary>One callback or complete instance definition supplied by code registration.</summary>
        private sealed record ProfileDefinition(string Name, Action<LogThisConfiguration>? Configure, LogThisConfiguration? Configuration);

        #endregion
    }
}
