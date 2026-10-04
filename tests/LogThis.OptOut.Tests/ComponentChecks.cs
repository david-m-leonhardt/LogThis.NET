using LogThis.Entities;
using LogThis.Extensions;
using LogThis.Interfaces;
using LogThis.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Checks template labels, reserved names, and the custom-component snapshot.</summary>
public static class ComponentChecks
{
    #region Public Methods

    /// <summary>Runs checks against raw provider state and each supported configuration path.</summary>
    public static void Run()
    {
        CheckLabels();
        CheckReservations();
        CheckMutation();
    }

    #endregion

    #region Private Methods

    /// <summary>Labels change only templates; event fields and their order remain unchanged.</summary>
    private static void CheckLabels()
    {
        var sink = new CorrelationLogger();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(sink));
        var options = new LogThisConfiguration
        {
            LogClassName = true, LogMethodArguments = true, LogMethodReturnValue = true,
            MessageDelimeter = " / "
        };
        Check(options.LogComponentLabels, "Labels must default to enabled.");
        options.AddMessageComponents(new() { ["Application"] = "test" });
        var factory = new LogThisScopeFactory(loggerFactory, options);
        using var scope = factory.BeginScope("labels", () => "AbCdEf0123456789Z");
        var context = LogThisMethodContext.FromNamedArguments("SubmitAsync", "OrderService", [new("value", 7)]);
        var failure = new InvalidOperationException("original");
        foreach (bool labels in new[] { true, false })
        {
            options.LogComponentLabels = labels;
            LogThisRuntimeLogger.Entry(context);
            LogThisRuntimeLogger.Exit(context, 8, 12.5);
            LogThisRuntimeLogger.Exception(context, failure, 12.5);
        }
        var events = sink.Events.ToArray();
        var messages = sink.Messages.ToArray();
        Check(events.Length == 6, "Label setting changed event selection.");
        for (int i = 0; i < 3; i++)
            Check(events[i].Where(p => p.Key != "{OriginalFormat}").SequenceEqual(events[i + 3].Where(p => p.Key != "{OriginalFormat}")), "Labels changed structured names, values, or order.");
        Check(messages[0] == "Entered / Correlation ID: AbCdEf0123456789Z / Class: OrderService / Method: SubmitAsync / Arguments: {\"value\":7} / test", "Default entry labels incorrect.");
        Check(messages[1] == "Exited / Correlation ID: AbCdEf0123456789Z / Duration ms: 12.5 / Class: OrderService / Method: SubmitAsync / Return value: 8 / test", "Exit labels incorrect.");
        Check(messages[2] == "Exception / Correlation ID: AbCdEf0123456789Z / Duration ms: 12.5 / Class: OrderService / Method: SubmitAsync / Arguments: {\"value\":7} / Return value:  / test", "Exception labels incorrect.");
        Check(messages[3] == "Entered / AbCdEf0123456789Z / OrderService / SubmitAsync / {\"value\":7} / test", "Disabled entry labels incorrect.");
        Check(messages[4] == "Exited / AbCdEf0123456789Z / 12.5 / OrderService / SubmitAsync / 8 / test", "Disabled exit labels incorrect.");
        Check(messages[5] == "Exception / AbCdEf0123456789Z / 12.5 / OrderService / SubmitAsync / {\"value\":7} /  / test", "Disabled exception labels incorrect.");
        Check(Equals(events[0].Single(p => p.Key == "LogThisArguments").Value, "{\"value\":7}"), "Argument value contains a label.");
        Check(Equals(events[1].Single(p => p.Key == "LogThisReturnValue").Value, "8"), "Return value contains a label.");
        Check(events[1].Single(p => p.Key == "LogThisDurationMs").Value is double, "Duration lost its numeric type.");
        options.OnEntryConfig.LogAccessPoint = false;
        LogThisRuntimeLogger.Entry(context);
        Check(sink.Events.Count == 6, "Labels enabled a suppressed event.");
    }

    /// <summary>Every generated name is reserved through every registration route, even with features disabled.</summary>
    private static void CheckReservations()
    {
        string[] reserved = ["LogThisOnEntry", "LogThisOnExit", "LogThisOnException", "LogThisClass", "LogThisMethod", "LogThisArguments", "LogThisReturnValue", "LogThisDurationMs", "LogThisCorrelationId", "LogThisTraceId", "LogThisSpanId"];
        foreach (string name in reserved)
        {
            foreach (string key in new[] { name, "{" + name.ToLowerInvariant() + "}", name.ToUpperInvariant(), "{" + name + ",10}", "{" + name + ":N2}", "{@" + name + "}", "{$" + name + "}" })
            {
                Reject(() => new LogThisConfiguration().AddMessageComponents(new() { [key] = 1 }), key);
                foreach (int route in Enumerable.Range(0, 4))
                {
                    // A colon denotes hierarchy in IConfiguration, so formatted placeholders are
                    // tested through dictionary/callback paths instead of inventing a flat binder key.
                    if (key.Contains(':') && route < 2) continue;
                    Reject(() =>
                    {
                        var services = new ServiceCollection();
                        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                            new Dictionary<string, string?> { ["LogThis:MessageComponents:" + key] = "bad" }).Build();
                        if (route == 0) services.AddLogThisConfiguration(configuration.GetSection("LogThis"));
                        if (route == 1)
                        {
                            services.AddSingleton<IConfiguration>(configuration);
                            services.AddLogThisConfiguration();
                        }
                        if (route == 2) services.AddLogThisConfiguration(o => o.MessageComponents[key] = 1);
                        if (route == 3)
                        {
                            var options = new LogThisConfiguration { LogCorrelationId = false, LogMethodName = false };
                            options.OnEntryConfig.LogAccessPoint = false;
                            options.OnExitConfig.LogAccessPoint = false;
                            options.OnExceptionConfig.LogAccessPoint = false;
                            options.MessageComponents[key] = 1;
                            services.AddLogThisConfiguration(options);
                        }
                        using var provider = services.BuildServiceProvider();
                        provider.GetRequiredService<ILogThisConfiguration>();
                    }, key);
                }
            }
        }
        foreach (string invalid in new[] { "{{LogThisMethod}}", "{LogThisClass}{Other}", "LogThisMethod}", "{LogThisMethod" })
            Reject(() => invalid.PrepComponentName(), invalid);
        foreach (string allowed in new[] { "Application", "{Tenant}", "LogThisCustom", "Method", "CorrelationId", "{Amount,10:N2}" })
            Check(allowed.PrepComponentName().StartsWith('{'), "Nonconflicting name rejected.");

        var binding = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LogThis:LogComponentLabels"] = "false", ["LogThis:MessageComponents:Application"] = "bound"
        }).Build();
        var boundServices = new ServiceCollection();
        boundServices.AddLogThisConfiguration(binding.GetSection("LogThis"));
        using var boundProvider = boundServices.BuildServiceProvider();
        Check(!boundProvider.GetRequiredService<ILogThisConfiguration>().LogComponentLabels, "Label binding failed.");
        var callbackServices = new ServiceCollection();
        callbackServices.AddSingleton<IConfiguration>(binding);
        callbackServices.AddLogThisConfiguration(o => o.LogComponentLabels = true);
        using var callbackProvider = callbackServices.BuildServiceProvider();
        Check(callbackProvider.GetRequiredService<ILogThisConfiguration>().LogComponentLabels, "Label callback override failed.");
    }

    /// <summary>Later dictionary edits cannot replace fields in existing or future scopes from the same factory.</summary>
    private static void CheckMutation()
    {
        var sink = new CorrelationLogger();
        var options = new LogThisConfiguration { LogCorrelationId = false };
        options.MessageComponents["Application"] = "initial";
        options.AddMessageComponents(new() { ["LogThisCustom"] = 42, ["CorrelationId"] = "application ID" });
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(options);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILogThisScopeFactory>();
        using (factory.BeginScope("first"))
        {
            options.MessageComponents.Clear();
            options.MessageComponents["{logthismethod,10}"] = "injected";
            options.MessageComponents["Application"] = "changed";
            options.AddMessageComponents(new() { ["Later"] = "ignored" });
            new DurationSample().Success();
        }
        using (factory.BeginScope("second")) new DurationSample().Success();
        Check(sink.Events.Count == 4, "Mutation suppressed events.");
        foreach (var entry in sink.Events)
        {
            Check(entry.Count(p => p.Key.Equals("LogThisMethod", StringComparison.OrdinalIgnoreCase)) == 1, "Mutation injected a reserved field.");
            Check(Equals(entry.Single(p => p.Key == "Application").Value, "initial"), "Snapshot changed.");
            Check(entry.All(p => p.Key != "Later") && entry.Any(p => p.Key == "LogThisCustom") && entry.Any(p => p.Key == "CorrelationId"), "Allowed custom fields or snapshot policy changed.");
        }
        Reject(() => options.AddMessageComponents(new() { ["LogThisMethod"] = "bad" }), "LogThisMethod");

        // Edits between options resolution and factory creation are revalidated, not silently accepted.
        var lateServices = new ServiceCollection();
        lateServices.AddLogging();
        lateServices.AddLogThisConfiguration();
        using var lateProvider = lateServices.BuildServiceProvider();
        lateProvider.GetRequiredService<ILogThisConfiguration>().MessageComponents["LogThisArguments"] = "bad";
        Reject(() => lateProvider.GetRequiredService<ILogThisScopeFactory>(), "LogThisArguments");
    }

    /// <summary>Requires an actionable configuration error naming the conflicting input.</summary>
    private static void Reject(Action action, string key)
    {
        try { action(); }
        catch (ArgumentException exception)
        {
            Check(exception.Message.Contains(key, StringComparison.OrdinalIgnoreCase) && exception.Message.Contains("Choose another name"), "Configuration error was not actionable.");
            return;
        }
        throw new Exception("Accepted conflicting component: " + key);
    }

    /// <summary>Reports a failed check.</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    #endregion
}
