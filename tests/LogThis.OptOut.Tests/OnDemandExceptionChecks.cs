using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Interfaces;
using LogThis.Middleware;
using LogThis.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

public static class OnDemandExceptionChecks
{
    public static void Run()
    {
        CheckOverrideInitialization();
        var failure = new InvalidOperationException("original");
        LogOnDemand.LogThisExceptionNow(failure, profile: "missing");

        var sink = new DurationLogger();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LogThis:OnExceptionConfig:AccessPointMessage"] = "default failure",
            ["LogThis:OnExceptionConfig:LogLevel"] = "Warning",
            ["LogThis:Profiles:Bound:OnExceptionConfig:LogAccessPoint"] = "false",
            ["LogThis:Profiles:Bound:OnDemandExceptionConfig:AccessPointMessage"] = "bound override",
            ["LogThis:Profiles:Empty:OnDemandExceptionConfig"] = null
        }).Build());
        services.AddLogThisConfiguration(o =>
        {
            o.LogClassName = true;
            o.LogMethodDuration = true;
            o.LogMethodReturnValue = true;
        });
        services.AddLogThisProfile("Named", o =>
        {
            o.OnExceptionConfig.AccessPointMessage = "named failure";
            o.LogMethodName = false;
        });
        services.AddLogThisProfile("Disabled", o => o.OnExceptionConfig.LogAccessPoint = false);
        services.AddLogThisProfile("Filtered", o => o.OnExceptionConfig.LogLevel = LogLevel.Trace);
        services.AddLogThisProfile("Bound", options =>
        {
            IAccessPointConfiguration? bound = options.OnDemandExceptionConfig;
            Check(bound != null, "Override was not bound before the registration callback.");
            options.InitializeOnDemandExceptionConfiguration();
            Check(ReferenceEquals(bound, options.OnDemandExceptionConfig) &&
                options.OnDemandExceptionConfig.AccessPointMessage == "bound override",
                "Initializer replaced the bound override.");
        });
        services.AddLogThisProfile("Initialized", o =>
        {
            o.OnExceptionConfig.LogAccessPoint = false;
            o.InitializeOnDemandExceptionConfiguration().LogLevel = LogLevel.Warning;
        });
        using var provider = services.BuildServiceProvider();
        Check(provider.GetRequiredService<ILogThisConfiguration>().OnDemandExceptionConfig == null, "Absent override was initialized.");
        using var scope = provider.UseLogThis();
        var runtime = LogThisRuntimeContext.Current;

        LogOnDemand.LogThisExceptionNow(failure, "literal {value}", values: [new { OrderId = 42 }, null, new ProfilePayload()]);
        var first = sink.Events.Single();
        Check(ReferenceEquals(first.Failure, failure) && first.Level == LogLevel.Warning, "Exception identity or fallback level lost.");
        Check(Equals(first.Fields["LogThisOnDemandException"], "default failure") && !first.Fields.ContainsKey("LogThisOnException"), "Explicit event identity or fallback text lost.");
        Check(Equals(first.Fields["LogThisMessage"], "literal {value}") && Equals(first.Fields["LogThisMethod"], nameof(Run)), "Message or caller lost.");
        Check(!first.Fields.ContainsKey("LogThisClass") && !first.Fields.ContainsKey("LogThisDurationMs") && !first.Fields.ContainsKey("LogThisReturnValue"), "Unexpected method component.");
        var captured = JArray.Parse((string)first.Fields["LogThisArguments"]!);
        Check((int?)captured[0]["OrderId"] == 42 && captured[1].Type == JTokenType.Null, "Supplied values lost.");
        Check((string?)captured[2]["Explicit"] == "[FIXED]" && captured[2]["Excluded"] == null, "Capture safeguards bypassed.");

        using (LogThisRuntimeLogger.SelectProfile("Named"))
        {
            var named = LogThisRuntimeContext.Current;
            LogOnDemand.LogThisExceptionNow(null, "  ", profile: "");
            var e = sink.Events.Last();
            Check(e.Failure == null && Equals(e.Fields["LogThisOnDemandException"], "default failure") &&
                !e.Fields.ContainsKey("LogThisMessage") && !e.Fields.ContainsKey("LogThisArguments"), "Null exception or default selection failed.");
            Check(ReferenceEquals(named, LogThisRuntimeContext.Current), "Default selection leaked.");
        }
        LogOnDemand.LogThisExceptionNow(failure, profile: "Named");
        var namedEvent = sink.Events.Last();
        Check(Equals(namedEvent.Fields["LogThisOnDemandException"], "named failure") && !namedEvent.Fields.ContainsKey("LogThisMethod"), "Named fallback or caller setting lost.");
        Check(Equals(namedEvent.Fields["LogThisCorrelationId"], first.Fields["LogThisCorrelationId"]), "Correlation changed.");
        LogOnDemand.LogThisExceptionNow(failure, profile: "Bound");
        Check(sink.Events.Last().Level == LogLevel.Error && Equals(sink.Events.Last().Fields["LogThisOnDemandException"], "bound override"), "Override merged with disabled fallback or lost its defaults.");
        LogOnDemand.LogThisExceptionNow(failure, profile: "Empty");
        Check(Equals(sink.Events.Last().Fields["LogThisOnDemandException"], "Exception"), "Empty override did not use exception defaults.");
        LogOnDemand.LogThisExceptionNow(failure, profile: "Initialized");
        Check(sink.Events.Last().Level == LogLevel.Warning &&
            Equals(sink.Events.Last().Fields["LogThisOnDemandException"], "Exception"),
            "Initialized override did not replace the disabled fallback.");

        int count = sink.Events.Count;
        foreach (string name in new[] { "Disabled", "Filtered" })
        {
            int reads = OnDemandThrowingCapture.Reads;
            LogOnDemand.LogThisExceptionNow(failure, profile: name, values: [new OnDemandThrowingCapture()]);
            Check(OnDemandThrowingCapture.Reads == reads && sink.Events.Count == count, "Suppressed event attempted capture or delivery.");
        }
        try { LogOnDemand.LogThisExceptionNow(failure, profile: "named"); throw new Exception("Unknown profile accepted."); }
        catch (InvalidOperationException) { }
        LogOnDemand.LogThisExceptionNow(failure, values: [new OnDemandThrowingCapture()]);
        Check(sink.Events.Count == count, "Capture failure delivered an event.");
        sink.ThrowOnLog = true;
        LogOnDemand.LogThisExceptionNow(failure, profile: "Named");
        sink.ThrowOnLog = false;
        Check(ReferenceEquals(runtime, LogThisRuntimeContext.Current), "Exception logging leaked scope.");

        sink.Events.Clear();
        Sample(failure, rethrow: false);
        Check(sink.Events.Count(e => e.Fields.ContainsKey("LogThisOnDemandException")) == 1 &&
            sink.Events.All(e => !e.Fields.ContainsKey("LogThisOnException")), "Handled exception produced an AOP exception event.");
        sink.Events.Clear();
        try { Sample(failure, rethrow: true); throw new Exception("Rethrow was swallowed."); }
        catch (InvalidOperationException e) when (ReferenceEquals(e, failure)) { }
        var failures = sink.Events.Where(e => e.Failure != null).ToArray();
        Check(failures.Length == 2 && failures.All(e => ReferenceEquals(e.Failure, failure)) &&
            failures[0].Fields.ContainsKey("LogThisOnDemandException") && failures[1].Fields.ContainsKey("LogThisOnException"),
            "Explicit logging and rethrow did not produce both original-exception events.");
        scope.Dispose();
        count = sink.Events.Count;
        LogOnDemand.LogThisExceptionNow(failure, profile: "missing");
        Check(sink.Events.Count == count, "No-scope call logged.");
    }

    private static void CheckOverrideInitialization()
    {
        var options = new LogThisConfiguration();
        options.OnExceptionConfig.AccessPointMessage = "fallback";
        options.OnExceptionConfig.LogAccessPoint = false;
        options.OnExceptionConfig.LogLevel = LogLevel.Warning;
        Check(options.OnDemandExceptionConfig == null, "Override should start absent.");

        options.InitializeOnDemandExceptionConfiguration();
        Check(options.OnDemandExceptionConfig.LogAccessPoint, "Initialized override was disabled.");
        IAccessPointConfiguration initialized = options.OnDemandExceptionConfig;
        Check(ReferenceEquals(initialized, options.OnDemandExceptionConfig) &&
            !ReferenceEquals(initialized, options.OnExceptionConfig), "Initializer did not install an independent override.");
        Check(initialized.LogAccessPoint && initialized.LogLevel == LogLevel.Error &&
            initialized.AccessPointMessage == "Exception", "Initializer did not use exception defaults.");
        initialized.AccessPointMessage = "custom override";
        Check(ReferenceEquals(initialized, options.InitializeOnDemandExceptionConfiguration()) &&
            initialized.AccessPointMessage == "custom override", "Repeated initialization reset the override.");
        Check(options.OnExceptionConfig.AccessPointMessage == "fallback" &&
            !options.OnExceptionConfig.LogAccessPoint && options.OnExceptionConfig.LogLevel == LogLevel.Warning,
            "Initializing the override changed fallback settings.");

        var supplied = new LogThisConfiguration().OnExceptionConfig;
        supplied.LogLevel = LogLevel.Critical;
        options.OnDemandExceptionConfig = supplied;
        Check(ReferenceEquals(supplied, options.InitializeOnDemandExceptionConfiguration()) &&
            supplied.LogLevel == LogLevel.Critical, "Initializer replaced a caller-supplied override.");
    }

    [LogThis]
    private static void Sample(Exception failure, bool rethrow)
    {
        try { throw failure; }
        catch (InvalidOperationException e)
        {
            LogOnDemand.LogThisExceptionNow(e);
            if (rethrow) throw;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
