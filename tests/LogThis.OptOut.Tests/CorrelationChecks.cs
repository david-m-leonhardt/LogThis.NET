using System.Collections.Concurrent;
using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Interfaces;
using LogThis.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Exercises correlation lifetime, inheritance, failure isolation, and structured events.</summary>
public static class CorrelationChecks
{
    #region Public Methods

    /// <summary>Runs correlation checks outside any ambient operation.</summary>
    public static async Task RunAsync()
    {
        Check(LogThisRuntimeContext.Current == null, "An earlier scope leaked.");
        var sink = new CorrelationLogger();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(sink));
        var options = new LogThisConfiguration();
        var factory = new LogThisScopeFactory(loggerFactory, options);
        var sample = new DurationSample();
        var failure = new InvalidOperationException("original");
        Check(options.LogCorrelationId, "Correlation is not enabled by default.");
        using (factory.BeginScope("outer"))
        {
            var outer = LogThisRuntimeContext.Current!;
            Check(outer.CorrelationId == null && sink.Events.IsEmpty, "Scope creation generated an ID or event.");
            Check(sample.Success() == 42, "Result changed.");
            string id = outer.CorrelationId!;
            Check(id.Length == 16 && id.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9'), "Invalid ID format.");
            await sample.ValueAwait();
            await ParentCall();
            // Nested configuration cannot replace existing correlation state, even when disabled.
            var nestedSink = new CorrelationLogger();
            using var nestedLoggerFactory = LoggerFactory.Create(b => b.AddProvider(nestedSink));
            var nestedFactory = new LogThisScopeFactory(nestedLoggerFactory, new LogThisConfiguration { LogCorrelationId = false });
            using (nestedFactory.BeginScope("nested", () => throw new Exception("Nested generator called.")))
            {
                Check(LogThisRuntimeContext.Current!.CorrelationId == id, "Nested ID changed.");
                await Task.Yield();
                sample.Success();
                Check(nestedSink.Events.Count == 2 && nestedSink.Events.All(e => e.All(p => p.Key != "LogThisCorrelationId")), "Disabled event correlation was included.");
            }
            Check(ReferenceEquals(LogThisRuntimeContext.Current, outer), "Outer runtime was not restored.");
            try { sample.Fail(failure); throw new Exception("Failure swallowed."); }
            catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
            Check(sink.Events.All(e => Equals(e.Single(p => p.Key == "LogThisCorrelationId").Value, id)), "Events did not share full ID.");
            Check(new[] { "LogThisOnEntry", "LogThisOnExit", "LogThisOnException" }.All(key => sink.Events.Any(e => e.Any(p => p.Key == key))), "Missing configured event.");
            Check(sink.Messages.All(m => m.Contains(id)), "Displayed ID was abbreviated.");
            sink.Events.Clear();
            loggerFactory.CreateLogger("application").LogInformation("explicit");
            Check(sink.Events.Single().All(p => p.Key != "LogThisCorrelationId"), "Explicit logging was enriched.");
            sink.Events.Clear();
            options.OnEntryConfig.LogAccessPoint = false;
            options.LogMethodDuration = true;
            sample.Success();
            Check(sink.Events.Count == 1 && sink.Events.Single().Any(p => p.Key == "LogThisDurationMs"), "Correlation changed event/duration selection.");
            sink.Events.Clear();
            sample.Excluded();
            Check(sink.Events.IsEmpty, "Excluded method emitted events.");
        }
        Check(LogThisRuntimeContext.Current == null, "Outermost runtime was not restored.");
        options.OnEntryConfig.LogAccessPoint = true;
        options.LogMethodDuration = false;

        int generated = 0;
        options.LogCorrelationId = false;
        using (factory.BeginScope("disabled", () => { generated++; return "AbCdEf0123456789Z"; }))
        {
            Check(generated == 0, "Disabled scope generated an ID.");
            sample.Success();
            Check(generated == 0 && sink.Events.All(e => e.All(p => p.Key != "LogThisCorrelationId")), "Disabled events requested correlation.");
            sink.Events.Clear();
            options.LogCorrelationId = true;
            using (factory.BeginScope("nested", () => throw new Exception("Nested generator used.")))
            {
                sample.Success();
                Check(LogThisRuntimeContext.Current!.CorrelationId == "AbCdEf0123456789Z" && generated == 1, "Nested first use did not initialize shared state.");
            }
            sample.Success();
            Check(generated == 1 && sink.Events.All(e => e.Any(p => p.Key == "LogThisCorrelationId" && Equals(p.Value, "AbCdEf0123456789Z"))), "Returning to outer scope lost shared ID.");
        }
        sink.Events.Clear();
        generated = 0;
        foreach (bool debug in new[] { false, true })
        {
            options.DebugLogThis = debug;
            using (factory.BeginScope("failure", () => { generated++; throw new InvalidOperationException("generation failure"); }))
            {
                Check(LogThisRuntimeContext.Current!.CorrelationId == null, "Failed generation created an ID.");
                using (factory.BeginScope("nested failure", () => throw new Exception("Must not retry")))
                    Check(sample.Success() == 42, "Generation failure prevented execution.");
            }
        }
        Check(generated == 2 && sink.Events.All(e => e.All(p => p.Key != "LogThisCorrelationId")), "Failure handling retried or emitted an ID.");
        options.DebugLogThis = false;
        sink.Events.Clear();

        // Both operations start outside a scope, overlap, and retain their own state after awaiting.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int ready = 0;
        async Task<string> Operation()
        {
            using var operation = factory.BeginScope("concurrent");
            if (Interlocked.Increment(ref ready) == 2) release.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Check(LogThisRuntimeContext.Current!.CorrelationId == null, "Independent scope generated early.");
            sample.Success();
            return LogThisRuntimeContext.Current!.CorrelationId!;
        }
        string[] ids = await Task.WhenAll(Task.Run(Operation), Task.Run(Operation));
        Check(ids[0] != ids[1] && LogThisRuntimeContext.Current == null, "Independent scopes were not isolated.");
        foreach (string id in ids)
            Check(sink.Events.Count(e => e.Any(p => p.Key == "LogThisCorrelationId" && Equals(p.Value, id))) == 2, "Concurrent event ID mismatch.");
        sink.Events.Clear();

        // Simultaneous first events in a single inherited chain must share one attempt.
        foreach (bool failGeneration in new[] { false, true })
        {
            generated = 0;
            using (factory.BeginScope("shared", () =>
            {
                Interlocked.Increment(ref generated);
                if (failGeneration) throw new InvalidOperationException("shared failure");
                return "AbCdEf0123456789Z";
            }))
            {
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Task[] calls = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
                {
                    await start.Task;
                    using var nested = factory.BeginScope("parallel child");
                    sample.Success();
                })).ToArray();
                Check(generated == 0, "Scope initialization was not lazy.");
                start.SetResult();
                await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(10));
                Check(generated == 1 && sink.Events.Count == 32, "Concurrent first use retried or lost events.");
                Check(sink.Events.All(e => failGeneration
                    ? e.All(p => p.Key != "LogThisCorrelationId")
                    : e.Any(p => p.Key == "LogThisCorrelationId" && Equals(p.Value, "AbCdEf0123456789Z"))), "Concurrent first-use state diverged.");
            }
            sink.Events.Clear();
        }

        // Event and provider filters (and failed capture) must leave correlation uninitialized.
        var filteredSink = new DurationLogger { Enabled = false };
        using var filteredLoggerFactory = LoggerFactory.Create(b => b.AddProvider(filteredSink));
        var filteredOptions = new LogThisConfiguration();
        var filteredFactory = new LogThisScopeFactory(filteredLoggerFactory, filteredOptions);
        generated = 0;
        using (filteredFactory.BeginScope("filtered", () => { generated++; return "AbCdEf0123456789Z"; }))
        {
            sample.Success();
            filteredSink.Enabled = true;
            filteredOptions.OnEntryConfig.LogAccessPoint = false;
            filteredOptions.OnExitConfig.LogAccessPoint = false;
            sample.Success();
            Check(generated == 0, "Filtered events generated correlation.");
            filteredOptions.OnEntryConfig.LogAccessPoint = true;
            filteredOptions.LogMethodArguments = true;
            LogThisRuntimeLogger.Entry(new LogThisMethodContext("capture", "test", [new ThrowingCapture()]));
            Check(generated == 0, "Failed serialization generated correlation.");
            filteredOptions.LogMethodArguments = false;
            sample.Success();
            Check(generated == 1 && filteredSink.Events.Count == 1, "First eligible event did not generate correlation.");
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["LogThis:LogCorrelationId"] = "false" }).Build();
        var services = new ServiceCollection();
        services.AddLogThisConfiguration(configuration.GetSection("LogThis"));
        using var provider = services.BuildServiceProvider();
        Check(!provider.GetRequiredService<ILogThisConfiguration>().LogCorrelationId, "Correlation binding failed.");
        var callbackServices = new ServiceCollection();
        callbackServices.AddSingleton<IConfiguration>(configuration);
        callbackServices.AddLogThisConfiguration(o => o.LogCorrelationId = true);
        using var callbackProvider = callbackServices.BuildServiceProvider();
        Check(callbackProvider.GetRequiredService<ILogThisConfiguration>().LogCorrelationId, "Correlation callback override failed.");
    }

    #endregion

    #region Private Methods

    /// <summary>Verifies that a woven parent and child share the ambient operation.</summary>
    [LogThis]
    private static async Task ParentCall()
    {
        await Task.Yield();
        new DurationSample().Success();
    }

    /// <summary>Reports a failed integration expectation.</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    #endregion
}

/// <summary>Records raw structured keys, including duplicates, across concurrent operations.</summary>
public sealed class CorrelationLogger : ILogger, ILoggerProvider
{
    #region Public Properties

    /// <summary>Raw event properties without dictionary normalization.</summary>
    public ConcurrentQueue<KeyValuePair<string, object?>[]> Events { get; } = new();
    /// <summary>Rendered event text.</summary>
    public ConcurrentQueue<string> Messages { get; } = new();

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => this;
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => throw new Exception("Logger scope enrichment must not be used.");
    /// <inheritdoc />
    public void Dispose() { }
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Events.Enqueue(((IEnumerable<KeyValuePair<string, object?>>)state!).ToArray());
        Messages.Enqueue(formatter(state, exception));
    }

    #endregion
}
