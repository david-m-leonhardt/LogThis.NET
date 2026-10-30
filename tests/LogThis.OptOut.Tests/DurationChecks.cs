using System.Diagnostics;
using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Interfaces;
using LogThis.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Checks optional duration fields without relying on scheduling thresholds.</summary>
public static class DurationChecks
{
    #region Public Methods

    /// <summary>Verifies configuration, completion timing, suppression, and application outcomes.</summary>
    public static async Task RunAsync()
    {
        var sink = new DurationLogger();
        var options = new LogThisConfiguration();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(sink));
        services.AddLogThisConfiguration(options);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var sample = new DurationSample();
        Check(!options.LogMethodDuration && LogThisRuntimeLogger.StartDuration() == null, "Timing defaults changed.");
        Check(sample.Success() == 42, "Result changed.");
        Check(sink.Events.Count == 2 && sink.Events.All(e => !e.Fields.ContainsKey("LogThisDurationMs")), "Default duration was logged.");
        sink.Events.Clear();

        options.LogMethodDuration = true;
        Check(sample.Success() == 42, "Timed result changed.");
        CheckCompletion(sink, "LogThisOnExit", LogLevel.Information);
        var failure = new InvalidOperationException("original");
        options.LogMethodDuration = false;
        try { sample.Fail(failure); throw new Exception("Disabled timing swallowed failure."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(sink.Events.Count == 2 && sink.Events.All(e => !e.Fields.ContainsKey("LogThisDurationMs")), "Disabled duration was logged.");
        sink.Events.Clear();
        options.LogMethodDuration = true;
        try { sample.Fail(failure); throw new Exception("Failure swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(ReferenceEquals(sink.Events.Last().Failure, failure), "Logged exception changed.");
        CheckCompletion(sink, "LogThisOnException", LogLevel.Error);

        // A gate establishes that completion logging occurs only after the awaited work completes.
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> pending = sample.Await(gate.Task);
        Check(!pending.IsCompleted && sink.Events.Count == 1, "Async method logged completion before await.");
        long beforeRelease = Stopwatch.GetTimestamp();
        gate.SetResult(73);
        Check(await pending.WaitAsync(TimeSpan.FromSeconds(10)) == 73, "Awaited result changed.");
        double duration = (double)sink.Events.Last().Fields["LogThisDurationMs"]!;
        Check(duration >= Stopwatch.GetElapsedTime(sample.AwaitStarted, beforeRelease).TotalMilliseconds, "Invalid duration.");
        CheckCompletion(sink, "LogThisOnExit", LogLevel.Information);

        var failingGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> failing = sample.Await(failingGate.Task);
        Check(sink.Events.Count == 1, "Async failure logged early.");
        failingGate.SetException(failure);
        try { await failing.WaitAsync(TimeSpan.FromSeconds(10)); throw new Exception("Async failure swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        CheckCompletion(sink, "LogThisOnException", LogLevel.Error);
        Check(await sample.ValueAwait() == 19, "ValueTask result changed.");
        CheckCompletion(sink, "LogThisOnExit", LogLevel.Information);

        options.OnExitConfig.LogAccessPoint = false;
        sample.Success();
        Check(sink.Events.Count == 1 && sink.Events[0].Fields.ContainsKey("LogThisOnEntry"), "Suppressed exit emitted.");
        sink.Events.Clear();
        options.OnExceptionConfig.LogAccessPoint = false;
        try { sample.Fail(failure); } catch (InvalidOperationException) { }
        Check(sink.Events.Count == 1, "Suppressed exception emitted an event.");
        sink.Events.Clear();
        options.OnEntryConfig.LogAccessPoint = false;
        sample.Success();
        Check(sink.Events.Count == 0, "Suppressed events emitted.");
        options.OnExitConfig.LogAccessPoint = true;
        sample.Success();
        Check(sink.Events.Count == 1 && sink.Events[0].Fields["LogThisDurationMs"] is double, "Entry suppression prevented duration.");
        sink.Events.Clear();
        sample.Excluded();
        Check(sink.Events.Count == 0, "Excluded method emitted an event.");
        sink.Enabled = false;
        sample.Success();
        Check(sink.Events.Count == 0, "Disabled level emitted an event.");
        sink.Enabled = true;
        sink.ThrowOnLog = true;
        Check(sample.Success() == 42, "Logger failure changed result.");
        try { sample.Fail(failure); throw new Exception("Logger swallowed failure."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["LogThis:LogMethodDuration"] = "true" }).Build();
        var boundServices = new ServiceCollection();
        boundServices.AddLogThisConfiguration(configuration.GetSection("LogThis"));
        using var boundProvider = boundServices.BuildServiceProvider();
        Check(boundProvider.GetRequiredService<ILogThisConfiguration>().LogMethodDuration, "Duration did not bind.");
        var callbackServices = new ServiceCollection();
        callbackServices.AddSingleton<IConfiguration>(configuration);
        callbackServices.AddLogThisConfiguration(o => o.LogMethodDuration = false);
        using var callbackProvider = callbackServices.BuildServiceProvider();
        Check(!callbackProvider.GetRequiredService<ILogThisConfiguration>().LogMethodDuration, "Callback override failed.");
    }

    #endregion

    #region Private Methods

    /// <summary>Checks the normal entry/completion pair and clears recorded events.</summary>
    private static void CheckCompletion(DurationLogger sink, string component, LogLevel level)
    {
        Check(sink.Events.Count == 2, "Unexpected event count.");
        Check(!sink.Events[0].Fields.ContainsKey("LogThisDurationMs"), "Entry contains duration.");
        var completion = sink.Events[1];
        Check(completion.Fields.ContainsKey(component) && completion.Level == level, "Completion kind or severity changed.");
        Check(completion.Fields["LogThisDurationMs"] is double value && double.IsFinite(value) && value >= 0, "Duration is not numeric elapsed milliseconds.");
        Check(completion.Fields["{OriginalFormat}"] is string template && template.Contains("Duration ms: {LogThisDurationMs}"), "Duration display label is missing.");
        sink.Events.Clear();
    }

    /// <summary>Fails the executable checks with a focused message.</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    #endregion
}

/// <summary>Methods woven through the same pipeline as application code.</summary>
[LogThis]
public sealed class DurationSample
{
    #region Public Fields

    /// <summary>Timestamp within the method used to bound the awaited interval.</summary>
    public long AwaitStarted;

    #endregion

    #region Public Methods

    /// <summary>Returns a synchronous result.</summary>
    public int Success() => 42;
    /// <summary>Throws the supplied exception unchanged.</summary>
    public void Fail(Exception failure) => throw failure;
    /// <summary>Waits for application-controlled completion.</summary>
    public async Task<int> Await(Task<int> gate) { AwaitStarted = Stopwatch.GetTimestamp(); return await gate; }
    /// <summary>Checks the ValueTask template.</summary>
    public async ValueTask<int> ValueAwait() { await Task.Yield(); return 19; }
    /// <summary>Opts out of all generated events.</summary>
    [DontLogThis]
    public void Excluded() { }

    #endregion
}

/// <summary>Records structured fields and can simulate provider failures.</summary>
public sealed class DurationLogger : ILogger, ILoggerProvider
{
    #region Public Properties

    /// <summary>Captured event state before rendering.</summary>
    public List<(LogLevel Level, Dictionary<string, object?> Fields, Exception? Failure)> Events { get; } = [];
    /// <summary>Controls the provider's level check.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Simulates an emission failure.</summary>
    public bool ThrowOnLog { get; set; }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => this;
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => Enabled;
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    /// <inheritdoc />
    public void Dispose() { }
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (ThrowOnLog) throw new InvalidOperationException("provider failure");
        Events.Add((logLevel, ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(), exception));
    }

    #endregion
}
