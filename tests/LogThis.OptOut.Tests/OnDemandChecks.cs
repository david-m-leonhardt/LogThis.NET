using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using LogThis.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

public static class OnDemandChecks
{
    public static void Run()
    {
        LogOnDemand.LogThisNow(profile: "missing");
        var sink = new CorrelationLogger();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(o => { o.LogClassName = true; o.LogMethodReturnValue = true; o.LogTraceId = true; o.LogSpanId = true; });
        foreach (string name in new[] { "Named", "named", " Named ", " " })
            services.AddLogThisProfile(name, o => { o.JsonMaskValue = name; o.AddMessageComponents(new() { ["Selected"] = name }); });
        services.AddLogThisProfile("NoMember", o => o.LogMethodName = false);
        services.AddLogThisProfile("Disabled", o => o.OnDemandConfig.LogAccessPoint = false);
        services.AddLogThisProfile("Filtered", o => o.OnDemandConfig.LogLevel = LogLevel.Trace);
        services.AddLogThisProfile("Short", o => o.MaxCapturedValueLength = 12);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        using var activity = new Activity("on-demand").SetIdFormat(ActivityIdFormat.W3C).Start();
        var original = LogThisRuntimeContext.Current;
        LogOnDemand.LogThisNow();
        var first = sink.Events.Last();
        Check(Equals(Field(first, "LogThisOnDemand"), "OnDemand"), "Parameterless event missing.");
        Check(Equals(Field(first, "LogThisMethod"), nameof(Run)), "Caller member missing.");
        Check(Field(first, "LogThisClass") == null && Field(first, "LogThisReturnValue") == null && !sink.Messages.Last().Contains("Class:"), "Unexpected class/return component.");
        Check(Equals(Field(first, "LogThisTraceId"), activity.TraceId.ToString()) && Equals(Field(first, "LogThisSpanId"), activity.SpanId.ToString()), "Activity lost.");
        string id = (string)Field(first, "LogThisCorrelationId")!;
        LogOnDemand.LogThisNow("literal {braces}");
        Check(Equals(Field(sink.Events.Last(), "LogThisMessage"), "literal {braces}") && sink.Messages.Last().Contains("literal {braces}"), "Message treated as template.");
        foreach (string? message in new string?[] { null, "", "  " })
        {
            int count = sink.Events.Count;
            LogOnDemand.LogThisNow(message);
            Check(sink.Events.Count == count + 1 && Field(sink.Events.Last(), "LogThisMessage") == null, "Blank message suppressed event.");
        }
        LogOnDemand.LogThisNow(values: [new { CustomerId = 42 }, null]);
        var data = JArray.Parse((string)Field(sink.Events.Last(), "LogThisArguments")!);
        Check((int?)data[0]["CustomerId"] == 42 && data[1].Type == JTokenType.Null, "Anonymous/null capture failed.");
        foreach (string name in new[] { "Named", "named", " Named ", " " })
        {
            LogOnDemand.LogThisNow(profile: name, values: [new ProfilePayload()]);
            var e = sink.Events.Last();
            Check(Equals(Field(e, "Selected"), name) && Equals(Field(e, "LogThisCorrelationId"), id), "Profile/correlation mismatch.");
            var payload = JArray.Parse((string)Field(e, "LogThisArguments")!)[0];
            Check((string?)payload["Attribute"] == name && (string?)payload["Explicit"] == "[FIXED]" && payload["Excluded"] == null, "Capture safeguards failed.");
            Check(ReferenceEquals(original, LogThisRuntimeContext.Current), "Profile leaked.");
        }
        using (LogThisRuntimeLogger.SelectProfile("Named"))
        {
            var named = LogThisRuntimeContext.Current;
            LogOnDemand.LogThisNow();
            Check(Field(sink.Events.Last(), "Selected") == null && ReferenceEquals(named, LogThisRuntimeContext.Current), "Default inherited surrounding profile.");
            new OnDemandAttributedSample().DefaultCall();
            Check(Field(sink.Events.Last(), "Selected") == null, "Attributed default selection failed.");
        }
        LogOnDemand.LogThisNow(profile: "NoMember");
        Check(Field(sink.Events.Last(), "LogThisMethod") == null, "Caller setting ignored.");
        LogOnDemand.LogThisNow(profile: "");
        Check(Field(sink.Events.Last(), "Selected") == null, "Empty selection not default.");
        foreach (string name in new[] { "NAMED", "Named ", "missing" })
        {
            try { LogOnDemand.LogThisNow(profile: name); throw new Exception("Missing profile accepted."); }
            catch (InvalidOperationException) { }
        }
        try { new OnDemandAttributedSample().Missing(); throw new Exception("Missing attributed profile accepted."); }
        catch (InvalidOperationException) { }
        foreach (string name in new[] { "Disabled", "Filtered" })
        {
            int count = sink.Events.Count;
            int reads = OnDemandThrowingCapture.Reads;
            LogOnDemand.LogThisNow(profile: name, values: [new OnDemandThrowingCapture()]);
            Check(OnDemandThrowingCapture.Reads == reads, "Disabled/filtered event performed capture.");
            Check(sink.Events.Count == count, "Disabled/filtered capture emitted.");
        }
        int before = sink.Events.Count;
        LogOnDemand.LogThisNow(values: [new OnDemandThrowingCapture()]);
        Check(sink.Events.Count == before, "Capture failure emitted partial event.");
        LogOnDemand.LogThisNow(profile: "Short", values: [new string('x', 50)]);
        Check((bool?)JObject.Parse((string)Field(sink.Events.Last(), "LogThisArguments")!)["truncated"] == true, "Truncation bypassed.");
        LogOnDemand.LogThisNow(values: [Deferred()]);
        Check(((string)Field(sink.Events.Last(), "LogThisArguments")!).Contains("Not enumerated"), "Iterator safeguard bypassed.");
        int writes = ProbeConverter<TypeProbe>.Writes;
        LogOnDemand.LogThisNow(values: [new TypeConverted()]);
        Check(ProbeConverter<TypeProbe>.Writes == writes, "Converter policy bypassed.");
        var failureServices = new ServiceCollection();
        failureServices.AddLogging(b => b.AddProvider(new DurationLogger { ThrowOnLog = true }));
        failureServices.AddLogThisConfiguration();
        using var failureProvider = failureServices.BuildServiceProvider();
        using (failureProvider.UseLogThis()) LogOnDemand.LogThisNow();
        Check(ReferenceEquals(original, LogThisRuntimeContext.Current), "Failure leaked scope.");
        scope.Dispose();
        before = sink.Events.Count;
        LogOnDemand.LogThisNow(values: [new OnDemandThrowingCapture()]);
        Check(sink.Events.Count == before, "No-scope call emitted.");
        // Configuration binding exposes the new event settings through the usual path.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["LogThis:OnDemandConfig:AccessPointMessage"] = "Manual", ["LogThis:OnDemandConfig:LogAccessPoint"] = "false" }).Build();
        var bound = new ServiceCollection();
        bound.AddLogThisConfiguration(configuration.GetSection("LogThis"));
        using var boundProvider = bound.BuildServiceProvider();
        var options = boundProvider.GetRequiredService<LogThis.Interfaces.ILogThisConfiguration>();
        Check(!options.OnDemandConfig.LogAccessPoint && options.OnDemandConfig.AccessPointMessage == "Manual", "OnDemand binding failed.");
    }
    private static IEnumerable<int> Deferred() { yield return Throw(); } private static int Throw() => throw new Exception("Enumerated.");
    private static object? Field(KeyValuePair<string, object?>[] e, string key) => e.FirstOrDefault(p => p.Key == key).Value;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
public sealed class OnDemandThrowingCapture
{
    public static int Reads;
    public string Value { get { Reads++; throw new Exception("Captured."); } }
}
public sealed class OnDemandAttributedSample
{
    [LogThis(Profile = "")] public void DefaultCall() { LogOnDemand.LogThisNow(); }
    [LogThis(Profile = "NAMED")] public void Missing() { throw new Exception("Body executed."); }
}
