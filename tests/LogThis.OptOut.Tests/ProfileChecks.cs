using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Interfaces;
using LogThis.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

/// <summary>Checks profile registration, weaving, and runtime configuration isolation.</summary>
public static class ProfileChecks
{
    #region Public Methods

    /// <summary>Runs profile checks through a real woven consumer.</summary>
    public static async Task RunAsync()
    {
        CheckRegistration();
        await CheckSelectionAsync();
        await CheckNoCorrelationAsync();
    }

    #endregion

    #region Private Methods

    /// <summary>Builds representative default and independent JSON profile definitions.</summary>
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["LogThis:LogClassName"] = "true",
        ["LogThis:JsonMaskValue"] = "default-mask",
        ["LogThis:OnEntryConfig:AccessPointMessage"] = "Default entry",
        ["LogThis:Profiles:Detailed:LogMethodArguments"] = "true",
        ["LogThis:Profiles:Detailed:JsonMaskValue"] = "detail-mask",
        ["LogThis:Profiles:Detailed:JsonFieldsToMask:0"] = "Configured",
        ["LogThis:Profiles:Detailed:MessageComponents:ProfileTag"] = "detail",
        ["LogThis:Profiles:ExternalCall:OnEntryConfig:LogAccessPoint"] = "false",
        ["LogThis:Profiles:ExternalCall:OnExceptionConfig:AccessPointMessage"] = "External call failed"
    }).Build();

    /// <summary>Checks independent defaults, binding, callbacks, supplied instances, and validation.</summary>
    private static void CheckRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Configuration());
        services.AddLogThisConfiguration();
        services.AddLogThisProfile(" detailed ", o => o.LogMethodDuration = true);
        services.AddLogThisProfile("CodeOnly", o => o.LogMethodReturnValue = true);
        var supplied = new LogThisConfiguration { LogMethodName = false };
        services.AddLogThisProfile("ExternalCall", supplied);
        using var provider = services.BuildServiceProvider();
        var profiles = provider.GetRequiredService<LogThisProfiles>();
        var defaultOptions = provider.GetRequiredService<ILogThisConfiguration>();
        var detailed = profiles.Select("DETAILED").Configuration;
        Check(defaultOptions.LogClassName && !defaultOptions.LogMethodDuration, "Profile modified defaults.");
        Check(detailed.LogMethodArguments && detailed.LogMethodDuration && !detailed.LogClassName && detailed.OnEntryConfig.AccessPointMessage == "Entered", "JSON/code layering or independent defaults failed.");
        Check(detailed.JsonMaskValue == "detail-mask" && detailed.JsonFieldsToMask.SequenceEqual(new[] { "Configured" }), "Profile binding lost mask settings.");
        Check(profiles.Select("CodeOnly").Configuration.JsonMaskValue == "*****", "Code-only profile inherited defaults.");
        Check(ReferenceEquals(profiles.Select("ExternalCall").Configuration, supplied) && supplied.OnEntryConfig.LogAccessPoint, "Supplied instance did not replace JSON definition.");
        Check(!ReferenceEquals(detailed.JsonFieldsToMask, defaultOptions.JsonFieldsToMask), "Profile collections were shared.");
        Reject(() => services.AddLogThisProfile("DETAILed", new LogThisConfiguration()), "duplicate code");
        Reject(() => services.AddLogThisProfile(" ", _ => { }), "empty name");
        Reject(() => services.AddLogThisProfile("", new LogThisConfiguration()), "empty instance name");

        foreach (string invalidKey in new[] { "UnknownSetting", "Profiles:Nested:LogMethodName", "MessageComponents:LogThisMethod" })
        {
            var invalid = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["LogThis:Profiles:Bad:" + invalidKey] = "true" }).Build();
            Reject(() =>
            {
                var invalidServices = new ServiceCollection();
                invalidServices.AddLogging();
                invalidServices.AddLogThisConfiguration(invalid.GetSection("LogThis"));
                using var invalidProvider = invalidServices.BuildServiceProvider();
                invalidProvider.GetRequiredService<ILogThisScopeFactory>();
            }, invalidKey);
        }
        foreach (bool callback in new[] { false, true })
        {
            Reject(() =>
            {
                var invalidServices = new ServiceCollection();
                invalidServices.AddLogging();
                if (callback) invalidServices.AddLogThisProfile("Bad", o => o.MessageComponents["LogThisClass"] = "bad");
                else
                {
                    var bad = new LogThisConfiguration();
                    bad.MessageComponents["LogThisClass"] = "bad";
                    invalidServices.AddLogThisProfile("Bad", bad);
                }
                using var invalidProvider = invalidServices.BuildServiceProvider();
                invalidProvider.GetRequiredService<ILogThisScopeFactory>();
            }, "reserved profile field");
        }
        var standaloneServices = new ServiceCollection();
        standaloneServices.AddLogThisProfile("Only", _ => { });
        using var standalone = standaloneServices.BuildServiceProvider();
        Check(standalone.GetRequiredService<ILogThisConfiguration>().LogMethodName, "Named-only registration removed default.");

        // Registering a profile before an explicit default registration retains both definitions.
        var reorderedServices = new ServiceCollection();
        reorderedServices.AddLogThisProfile("Before", o => o.LogMethodDuration = true);
        reorderedServices.AddLogThisConfiguration(new LogThisConfiguration { LogClassName = true });
        using var reordered = reorderedServices.BuildServiceProvider();
        Check(reordered.GetRequiredService<LogThisProfiles>().Select("Before").Configuration.LogMethodDuration && reordered.GetRequiredService<ILogThisConfiguration>().LogClassName, "Registration order changed definitions.");
    }

    /// <summary>Checks class/method precedence, selected capture configuration, and correlation identity.</summary>
    private static async Task CheckSelectionAsync()
    {
        var sink = new CorrelationLogger();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddSingleton(Configuration());
        services.AddLogThisConfiguration();
        services.AddLogThisProfile("Detailed", o => { o.LogMethodDuration = true; o.LogMethodReturnValue = true; });
        services.AddLogThisProfile("ExternalCall", o =>
        {
            o.LogCorrelationId = false;
            o.LogComponentLabels = false;
            o.JsonMaskValue = "external-mask";
            o.LogMethodArguments = true;
            o.LogMethodReturnValue = true;
            o.LogMethodDuration = true;
            o.AddMessageComponents(new() { ["ProfileTag"] = "external" });
        });
        using var provider = services.BuildServiceProvider();
        var sample = new ProfileSample();
        var payload = new ProfilePayload();
        // Unknown references are not validated outside an active operation.
        Check(sample.Missing() == 9, "No-scope behavior changed.");
        sample.MissingCalls = 0;
        using var scope = provider.UseLogThis();
        var originalRuntime = LogThisRuntimeContext.Current;
        Check(ReferenceEquals(await sample.Submit(payload, "excluded"), payload), "Profile changed result identity.");
        string id = originalRuntime!.CorrelationId!;
        var events = sink.Events.ToArray();
        Check(events.Length == 2, "Class profile was instrumented twice.");
        Check(events.All(e => Equals(Field(e, "ProfileTag"), "detail") && Equals(Field(e, "LogThisCorrelationId"), id) && !e.Any(p => p.Key == "LogThisClass")), "Class profile selection failed.");
        var arguments = JObject.Parse((string)Field(events[0], "LogThisArguments")!);
        Check(arguments["secret"] == null, "Excluded argument captured.");
        VerifyMask(arguments["value"]!, "detail-mask", true);
        VerifyMask(JToken.Parse((string)Field(events[1], "LogThisReturnValue")!), "detail-mask", true);
        Check(Field(events[1], "LogThisDurationMs") is double, "Profile duration missing.");
        Check(ReferenceEquals(LogThisRuntimeContext.Current, originalRuntime), "Method profile leaked into caller.");
        Clear(sink);
        sample.CheckStatus();
        Check(sink.Events.Count == 2 && sink.Events.All(e => e.Any(p => p.Key == "LogThisClass") && !e.Any(p => p.Key == "ProfileTag")), "Explicit default did not override class profile.");
        Check(Equals(Field(sink.Events.First(), "LogThisOnEntry"), "Default entry"), "Default entry message lost.");
        Clear(sink);
        sample.Ignore();
        sample.IgnoreBoth();
        new UnmarkedProfileSample().Run();
        Check(sink.Events.IsEmpty, "Excluded or unmarked method instrumented.");
        var failure = new InvalidOperationException("original");
        try { await sample.Send(payload, failure); throw new Exception("Failure swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(sink.Events.Count == 1, "External event configuration or single instrumentation failed.");
        var external = sink.Events.Single();
        Check(Equals(Field(external, "LogThisOnException"), "External call failed") && Field(external, "LogThisCorrelationId") == null && Field(external, "LogThisDurationMs") is double, "External completion configuration ignored.");
        VerifyMask(JObject.Parse((string)Field(external, "LogThisArguments")!)["value"]!, "external-mask", false);
        Check(!sink.Messages.Single().Contains("Method:"), "External labels ignored.");
        Check(LogThisRuntimeContext.Current!.CorrelationId == id, "Disabling profile correlation replaced chain state.");
        Clear(sink);
        await sample.Parent(payload);
        Check(sink.Events.Count == 4 && sink.Events.Count(e => Equals(Field(e, "LogThisMethod"), "CheckStatus")) == 2, "Caller profile affected child selection or duplicate instrumentation.");
        Check(sink.Events.Where(e => Equals(Field(e, "LogThisMethod"), "CheckStatus")).All(e => Field(e, "ProfileTag") == null), "Default child inherited parent profile.");
        Clear(sink);
        Reject(() => sample.Missing(), "missing profile");
        Check(sample.MissingCalls == 0 && sink.Events.IsEmpty, "Unknown profile silently fell back or executed body.");
        Check(ReferenceEquals(LogThisRuntimeContext.Current, originalRuntime), "Missing profile changed context.");
        // Both calls overlap under one operation, but hold different configurations across awaits.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object> a = sample.GatedDetailed(payload, gate.Task);
        Task<object> b = sample.GatedExternal(payload, gate.Task);
        gate.SetResult();
        await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(10));
        var exits = sink.Events.Where(e => e.Any(p => p.Key == "LogThisOnExit")).ToArray();
        Check(exits.Length == 2, "Concurrent completions missing.");
        foreach (var exit in exits)
        {
            bool detail = Equals(Field(exit, "ProfileTag"), "detail");
            VerifyMask(JToken.Parse((string)Field(exit, "LogThisReturnValue")!), detail ? "detail-mask" : "external-mask", detail);
            Check(detail ? Equals(Field(exit, "LogThisCorrelationId"), id) : Field(exit, "LogThisCorrelationId") == null, "Concurrent correlation emission leaked.");
        }
        Check(ReferenceEquals(LogThisRuntimeContext.Current, originalRuntime), "Concurrent selection leaked.");
        Check(payload.Attribute == "secret" && payload.Configured == "configured-secret", "Masking mutated model.");
    }

    /// <summary>An enabling profile initializes the chain lazily, and disabled methods preserve it for later use.</summary>
    private static async Task CheckNoCorrelationAsync()
    {
        var sink = new CorrelationLogger();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(o => o.LogCorrelationId = false);
        services.AddLogThisProfile("Detailed", o =>
        {
            o.LogCorrelationId = true;
            o.LogMethodReturnValue = true;
            o.JsonMaskValue = "second-provider";
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        Check(LogThisRuntimeContext.Current!.CorrelationId == null, "Disabled operation generated ID.");
        var transitionSample = new ProfileSample();
        transitionSample.CheckStatus();
        Check(sink.Events.All(e => e.All(p => p.Key != "LogThisCorrelationId")) && LogThisRuntimeContext.Current.CorrelationId == null, "Disabled method generated correlation.");
        Clear(sink);
        await transitionSample.Submit(new ProfilePayload(), "secret");
        string id = LogThisRuntimeContext.Current.CorrelationId!;
        Check(id != null && sink.Events.Count == 2 && sink.Events.All(e => Equals(Field(e, "LogThisCorrelationId"), id)), "Enabling profile failed to initialize correlation.");
        VerifyMask(JToken.Parse((string)Field(sink.Events.Last(), "LogThisReturnValue")!), "second-provider", false);
        Clear(sink);
        transitionSample.CheckStatus();
        Check(sink.Events.All(e => e.All(p => p.Key != "LogThisCorrelationId")) && LogThisRuntimeContext.Current.CorrelationId == id, "Disabled method cleared or emitted existing ID.");
        Clear(sink);
        await transitionSample.Submit(new ProfilePayload(), "secret");
        Check(sink.Events.All(e => Equals(Field(e, "LogThisCorrelationId"), id)), "Enabled method failed to reuse ID.");

        var failingServices = new ServiceCollection();
        failingServices.AddLogging(b => b.AddProvider(new DurationLogger { ThrowOnLog = true }));
        failingServices.AddLogThisProfile("Detailed", _ => { });
        failingServices.AddLogThisProfile("ExternalCall", _ => { });
        using var failingProvider = failingServices.BuildServiceProvider();
        using var failingScope = failingProvider.UseLogThis();
        var sample = new ProfileSample();
        var payload = new ProfilePayload();
        Check(ReferenceEquals(await sample.Submit(payload, "secret"), payload), "Provider failure changed named-profile result.");
        var failure = new InvalidOperationException("original");
        try { await sample.Send(payload, failure); throw new Exception("Provider failure swallowed method failure."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
    }

    /// <summary>Checks both attribute masks and per-profile configured name matching.</summary>
    private static void VerifyMask(JToken value, string mask, bool configured)
    {
        Check((string?)value["Attribute"] == mask && (string?)value["Explicit"] == "[FIXED]", "Profile attribute mask mismatch.");
        Check((string?)value["Configured"] == (configured ? mask : "configured-secret") && value["Excluded"] == null, "Profile configured mask/exclusion mismatch.");
    }

    private static object? Field(KeyValuePair<string, object?>[] fields, string name) => fields.FirstOrDefault(p => p.Key == name).Value;
    private static void Clear(CorrelationLogger sink) { sink.Events.Clear(); sink.Messages.Clear(); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string description)
    {
        try { action(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return; }
        throw new Exception("Expected configuration error: " + description);
    }

    #endregion
}

/// <summary>Class selection is overridden by explicit method declarations.</summary>
[LogThis(Profile = "Detailed")]
public sealed class ProfileSample
{
    #region Public Fields

    public int MissingCalls;

    #endregion

    #region Public Methods

    public async Task<object> Submit(object value, [DontLogThis] string secret) { await Task.Yield(); return value; }
    [LogThis] public void CheckStatus() { }
    [LogThis(Profile = "ExternalCall")]
    public async Task Send(object value, [DontLogThis] Exception failure) { await Task.Yield(); throw failure; }
    [DontLogThis] public void Ignore() { }
    [DontLogThis, LogThis(Profile = "Missing")] public void IgnoreBoth() { }
    public async Task Parent(object value) { await Task.Yield(); CheckStatus(); }
    [LogThis(Profile = "Missing")] public int Missing() { MissingCalls++; return 9; }
    public async Task<object> GatedDetailed(object value, [DontLogThis] Task gate) { await gate; return value; }
    [LogThis(Profile = "ExternalCall")]
    public async Task<object> GatedExternal(object value, [DontLogThis] Task gate) { await gate; return value; }

    #endregion
}

/// <summary>Models keep explicit replacements while other masks follow profile settings.</summary>
public sealed class ProfilePayload
{
    #region Public Properties

    [MaskThisInLogs] public string Attribute { get; } = "secret";
    [MaskThisInLogs("[FIXED]")] public string Explicit => throw new Exception("Masked getter read.");
    public string Configured { get; } = "configured-secret";
    [DontLogThis] public string Excluded => throw new Exception("Excluded getter read.");

    #endregion
}

/// <summary>Profiles alone do not instrument unmarked methods.</summary>
public sealed class UnmarkedProfileSample
{
    #region Public Methods

    public void Run() { }

    #endregion
}
