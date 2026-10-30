using System.Diagnostics;
using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Interfaces;
using LogThis.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Checks existing activity enrichment without tracing infrastructure.</summary>
public static class ActivityChecks
{
    #region Public Methods

    /// <summary>Runs configuration, context, presentation, and noninterference checks.</summary>
    public static async Task RunAsync()
    {
        var sink = new CorrelationLogger();
        var options = new LogThisConfiguration();
        Check(!options.LogTraceId && !options.LogSpanId, "Activity options must default to false.");
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(options);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var sample = new ActivitySample();
        var failure = new InvalidOperationException("original");
        Check(Activity.Current == null, "Unexpected test activity.");
        options.LogTraceId = options.LogSpanId = true;
        sample.Success();
        Verify(sink, null, true, true);
        sink.Events.Clear();
        using (var hierarchical = new Activity("legacy").SetIdFormat(ActivityIdFormat.Hierarchical).Start())
        {
            sample.Success();
            Verify(sink, null, true, true);
            Check(ReferenceEquals(Activity.Current, hierarchical), "Legacy activity changed.");
        }
        sink.Events.Clear();

        // Activity starts after the LogThis scope; enrichment cannot be cached on that scope.
        using (var parent = Start("parent"))
        {
            string correlation = LogThisRuntimeContext.Current!.CorrelationId!;
            foreach (bool trace in new[] { false, true })
            foreach (bool span in new[] { false, true })
            foreach (bool labels in new[] { true, false })
            {
                options.LogTraceId = trace;
                options.LogSpanId = span;
                options.LogComponentLabels = labels;
                sink.Events.Clear();
                sink.Messages.Clear();
                Check(await sample.Awaited() == 42, "Awaited result changed.");
                try { await sample.Fail(failure); throw new Exception("Exception swallowed."); }
                catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
                Check(sink.Events.Count == 4 && sink.Events.Last().Any(p => p.Key == "LogThisOnException"), "Event selection changed.");
                Verify(sink, parent, trace, span);
                foreach (string message in sink.Messages)
                {
                    Check(message.Contains("Trace ID:") == (trace && labels), "Trace label mismatch.");
                    Check(message.Contains("Span ID:") == (span && labels), "Span label mismatch.");
                }
                Check(sink.Events.All(e => Equals(Field(e, "LogThisCorrelationId"), correlation)), "Activity enrichment changed correlation identity.");
            }
            options.LogTraceId = options.LogSpanId = true;
            sink.Events.Clear();
            using (var child = Start("child"))
            {
                sample.Success();
                Verify(sink, child, true, true);
                Check(child.TraceId == parent.TraceId && child.SpanId != parent.SpanId, "Invalid nested activity fixture.");
            }
            sink.Events.Clear();
            sample.Excluded();
            Check(sink.Events.Count == 2 && sink.Events.All(e => Equals(Field(e, "LogThisMethod"), "Success")), "DontLogThis suppressed the eligible child or logged itself.");
            Verify(sink, parent, true, true);
            Check(ReferenceEquals(Activity.Current, parent) && parent.Duration == TimeSpan.Zero, "Logging stopped or replaced the parent activity.");
        }
        Check(Activity.Current == null, "Activity context was not restored.");
        sink.Events.Clear();
        sample.Success();
        Verify(sink, null, true, true);
        await CheckConcurrentAsync();
        CheckProfiles();
        CheckFiltering();
    }

    #endregion

    #region Private Methods

    /// <summary>Creates a controlled W3C activity without listeners or exporters.</summary>
    private static Activity Start(string name) => new Activity(name).SetIdFormat(ActivityIdFormat.W3C).Start();

    /// <summary>Compares raw structured values against the activity, including full hexadecimal lengths.</summary>
    private static void Verify(CorrelationLogger sink, Activity? activity, bool trace, bool span)
    {
        Check(!sink.Events.IsEmpty, "No events to inspect.");
        foreach (var entry in sink.Events)
        {
            object? traceId = Field(entry, "LogThisTraceId");
            object? spanId = Field(entry, "LogThisSpanId");
            Check(Equals(traceId, activity != null && trace ? activity.TraceId.ToHexString() : null), "Trace ID mismatch.");
            Check(Equals(spanId, activity != null && span ? activity.SpanId.ToHexString() : null), "Span ID mismatch.");
            if (traceId is string t) Check(t.Length == 32 && t.All(Uri.IsHexDigit), "Trace ID was shortened or malformed.");
            if (spanId is string s) Check(s.Length == 16 && s.All(Uri.IsHexDigit), "Span ID was shortened or malformed.");
        }
    }

    /// <summary>Concurrent flows retain their own current activities within an inherited LogThis scope.</summary>
    private static async Task CheckConcurrentAsync()
    {
        var sink = new CorrelationLogger();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(sink));
        var factory = new LogThisScopeFactory(loggerFactory, new LogThisConfiguration { LogTraceId = true, LogSpanId = true });
        using var scope = factory.BeginScope("parallel");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int ready = 0;
        async Task<(string Trace, string Span)> Operation()
        {
            using var activity = Start("independent");
            if (Interlocked.Increment(ref ready) == 2) release.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await new ActivitySample().Awaited();
            return (activity.TraceId.ToHexString(), activity.SpanId.ToHexString());
        }
        var ids = await Task.WhenAll(Task.Run(Operation), Task.Run(Operation));
        Check(ids[0].Trace != ids[1].Trace, "Independent test activities were not independent.");
        foreach (var id in ids)
            Check(sink.Events.Count(e => Equals(Field(e, "LogThisTraceId"), id.Trace) && Equals(Field(e, "LogThisSpanId"), id.Span)) == 2, "Concurrent activity context leaked.");
        Check(Activity.Current == null, "Concurrent child activity leaked to caller.");
    }

    /// <summary>Binding, named selection, code overrides, and reserved fields use existing infrastructure.</summary>
    private static void CheckProfiles()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LogThis:LogTraceId"] = "true", ["LogThis:LogSpanId"] = "true",
            ["LogThis:Profiles:SpanOnly:LogSpanId"] = "true"
        }).Build();
        var sink = new CorrelationLogger();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(config.GetSection("LogThis"));
        services.AddLogThisProfile("TraceOnly", o => o.LogTraceId = true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        using var activity = Start("profiles");
        var sample = new ActivitySample();
        sample.Success();
        Verify(sink, activity, true, true);
        sink.Events.Clear();
        sample.SpanOnly();
        Verify(sink, activity, false, true);
        sink.Events.Clear();
        sample.TraceOnly();
        Verify(sink, activity, true, false);
        foreach (string name in new[] { "LogThisTraceId", "LogThisSpanId" })
        {
            var invalid = new ServiceCollection();
            invalid.AddLogThisProfile("Invalid", o => o.MessageComponents[name] = "conflict");
            using var invalidProvider = invalid.BuildServiceProvider();
            try { invalidProvider.GetRequiredService<LogThisProfiles>(); throw new Exception("Reserved profile field accepted."); }
            catch (ArgumentException e) when (e.Message.Contains(name)) { }
        }
    }

    /// <summary>Event/level suppression and provider failures preserve method behavior and activity state.</summary>
    private static void CheckFiltering()
    {
        var sink = new DurationLogger { Enabled = false };
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(sink));
        var options = new LogThisConfiguration { LogTraceId = true, LogSpanId = true };
        using var scope = new LogThisScopeFactory(loggerFactory, options).BeginScope("filtered");
        using var activity = Start("filtered");
        var sample = new ActivitySample();
        sample.Success();
        sink.Enabled = true;
        options.OnEntryConfig.LogAccessPoint = options.OnExitConfig.LogAccessPoint = false;
        sample.Success();
        Check(sink.Events.Count == 0, "Enrichment enabled filtered events.");
        options.OnEntryConfig.LogAccessPoint = options.OnExitConfig.LogAccessPoint = true;
        sink.ThrowOnLog = true;
        Check(sample.Success() == 42 && ReferenceEquals(Activity.Current, activity), "Provider failure affected execution or activity.");
    }

    private static object? Field(KeyValuePair<string, object?>[] fields, string name) => fields.FirstOrDefault(p => p.Key == name).Value;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    #endregion
}

/// <summary>Exercises enrichment through real generated method events.</summary>
[LogThis]
public sealed class ActivitySample
{
    #region Public Methods

    /// <summary>Returns a value without creating spans.</summary>
    public int Success() => 42;
    /// <summary>Preserves the caller's activity through normal asynchronous completion.</summary>
    public async Task<int> Awaited() { await Task.Yield(); return 42; }
    /// <summary>Throws the original failure after an await.</summary>
    public async Task Fail(Exception failure) { await Task.Yield(); throw failure; }
    /// <summary>Excludes only this method; the child retains the activity.</summary>
    [DontLogThis] public void Excluded() => Success();
    /// <summary>Selects span-only JSON configuration.</summary>
    [LogThis(Profile = "SpanOnly")] public void SpanOnly() { }
    /// <summary>Selects trace-only code configuration.</summary>
    [LogThis(Profile = "TraceOnly")] public void TraceOnly() { }

    #endregion
}
