using System.Collections;
using System.Runtime.CompilerServices;
using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Interfaces;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Production iterator templates preserve creation, consumption, and disposal boundaries.</summary>
public static class IteratorTemplateChecks
{
    #region Public Methods

    /// <summary>Exercises all iterator forms through method and class attributes with real runtime scopes.</summary>
    public static async Task RunAsync()
    {
        var sink = new CorrelationLogger();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(o => o.JsonMaskValue = "outer");
        services.AddLogThisProfile("Iterator", o =>
        {
            o.LogMethodReturnValue = true;
            o.LogMethodDuration = true;
            o.JsonMaskValue = "iterator-mask";
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var outer = LogThisRuntimeContext.Current;
        var selected = provider.GetRequiredService<LogThisProfiles>().Select("Iterator").Configuration;
        foreach (var sample in new IProductionIteratorSample[] { new MethodIteratorSample(), new ClassIteratorSample() })
        foreach (bool capture in new[] { false, true })
        foreach (int form in Enumerable.Range(0, 6))
        foreach (string mode in new[] { "partial", "full", "failure", "cancel" })
        {
            if (mode == "cancel" && form < 4) continue;
            selected.LogMethodReturnValue = capture;
            sink.Events.Clear();
            var state = new IteratorState();
            if (mode == "failure") state.Failure = new InvalidOperationException("enumeration failure");
            using var cancellation = new CancellationTokenSource();
            // Enumerable cancellation is supplied at GetAsyncEnumerator; enumerator cancellation at the call.
            object result = Create(sample, form, state, form == 5 ? cancellation.Token : default);
            Check(state.Reads == 0 && state.Disposals == 0, "Iterator body ran at creation.");
            Check(ReferenceEquals(LogThisRuntimeContext.Current, outer), "Profile remained active after creation.");
            var events = sink.Events.ToArray();
            Check(events.Length == 2 && events[0].Any(p => p.Key == "LogThisOnEntry") && events[1].Any(p => p.Key == "LogThisOnExit"), "Creation event boundary changed.");
            Check(events[1].Any(p => p.Key == "LogThisDurationMs" && p.Value is double), "Creation duration missing.");
            Check(capture
                ? events[1].Any(p => p.Key == "LogThisReturnValue" && Equals(p.Value, "\"[Not enumerated]\""))
                : events[1].All(p => p.Key != "LogThisReturnValue"), "Iterator result capture was not passive or enabled itself.");
            Check(Equals(events[0].Single(p => p.Key == "LogThisCorrelationId").Value,
                events[1].Single(p => p.Key == "LogThisCorrelationId").Value), "Iterator correlation changed.");
            if (form < 4) Consume(result, state, mode);
            else await ConsumeAsync(result, state, mode, cancellation);
            Check(state.Disposals == 1 && state.ProfileSeen == "outer", "Disposal or profile restoration changed during consumption.");
            Check(sink.Events.Count == 2, "Enumeration emitted creation events or a misleading failure event.");
            Check(ReferenceEquals(sink.Events.Last(), events[1]), "Duration was replaced after consumption.");
            if (mode == "full" && form is 0 or 2 or 4)
            {
                state.Reads = state.Disposals = 0;
                if (form == 4) await ConsumeAsync(result, state, mode, cancellation);
                else Consume(result, state, mode);
                Check(state.Disposals == 1 && sink.Events.Count == 2, "Repeated enumeration changed behavior.");
            }
        }
        selected.LogMethodReturnValue = true;
        var extra = new IteratorCaptureSample();
        sink.Events.Clear();
        var maskedState = new IteratorState();
        _ = extra.Masked(maskedState);
        Check(maskedState.Reads == 0 && Equals(sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value, "\"iterator-mask\""), "Return mask lost precedence.");
        sink.Events.Clear();
        _ = extra.MaskedAsync(maskedState);
        Check(maskedState.Reads == 0 && Equals(sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value, "\"[ITERATOR]\""), "Async return mask lost precedence.");
        sink.Events.Clear();
        Check(extra.Excluded(maskedState).First() == 1 && sink.Events.IsEmpty, "DontLogThis no longer excludes iterators.");
        var list = new List<int> { 1, 2 };
        Check(ReferenceEquals(extra.Materialized(list), list), "Ordinary collection result identity changed.");
        Check(Equals(sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value, "[1,2]"), "Ordinary collection method was treated as a deferred iterator.");
        CheckPassiveCapture(sink);
    }

    #endregion

    #region Private Methods

    /// <summary>Calls a concrete iterator form without consuming it.</summary>
    private static object Create(IProductionIteratorSample sample, int form, IteratorState state, CancellationToken token) => form switch
    {
        0 => sample.Sequence(state), 1 => sample.Iterator(state),
        2 => sample.LegacySequence(state), 3 => sample.LegacyIterator(state),
        4 => sample.AsyncSequence(state), _ => sample.AsyncIterator(state, token)
    };

    /// <summary>Checks partial, full, or failing synchronous consumption and deterministic disposal.</summary>
    private static void Consume(object result, IteratorState state, string mode)
    {
        IEnumerator iterator = result is IEnumerable sequence ? sequence.GetEnumerator() : (IEnumerator)result;
        try
        {
            Check(iterator.MoveNext() && Equals(iterator.Current, 1) && state.Reads == 1, "First item timing changed.");
            if (mode == "partial") return;
            if (mode == "failure")
            {
                try { iterator.MoveNext(); throw new Exception("Enumeration failure swallowed."); }
                catch (InvalidOperationException e) when (ReferenceEquals(e, state.Failure)) { }
                return;
            }
            Check(iterator.MoveNext() && Equals(iterator.Current, 2) && state.Reads == 2 && !iterator.MoveNext(), "Full enumeration changed.");
        }
        finally { (iterator as IDisposable)?.Dispose(); }
    }

    /// <summary>Checks asynchronous consumption, token propagation, exception identity, and disposal.</summary>
    private static async Task ConsumeAsync(object result, IteratorState state, string mode, CancellationTokenSource cancellation)
    {
        await using var iterator = result is IAsyncEnumerable<int> sequence
            ? sequence.GetAsyncEnumerator(cancellation.Token) : (IAsyncEnumerator<int>)result;
        Check(await iterator.MoveNextAsync() && iterator.Current == 1 && state.Reads == 1, "First async item timing changed.");
        if (mode == "partial") return;
        if (mode == "failure")
        {
            try { await iterator.MoveNextAsync(); throw new Exception("Async failure swallowed."); }
            catch (InvalidOperationException e) when (ReferenceEquals(e, state.Failure)) { }
            return;
        }
        if (mode == "cancel")
        {
            cancellation.Cancel();
            try { await iterator.MoveNextAsync(); throw new Exception("Cancellation swallowed."); }
            catch (OperationCanceledException e) when (e.CancellationToken == cancellation.Token) { }
            Check(state.Reads == 1, "Cancellation requested another item.");
            return;
        }
        Check(await iterator.MoveNextAsync() && iterator.Current == 2 && state.Reads == 2 && !await iterator.MoveNextAsync(), "Full async enumeration changed.");
    }

    /// <summary>Unknown iterator values must never expose Current or execute move/dispose methods during capture.</summary>
    private static void CheckPassiveCapture(CorrelationLogger sink)
    {
        var options = new LogThisConfiguration { LogMethodArguments = true, LogMethodReturnValue = true };
        using var factory = LoggerFactory.Create(b => b.AddProvider(sink));
        using var scope = new LogThisScopeFactory(factory, options).BeginScope("passive");
        object[] values = [new UnreadEnumerator(), new UnreadAsyncIterator()];
        foreach (object value in values)
        {
            sink.Events.Clear();
            var context = LogThisMethodContext.FromNamedArguments("Capture", "Iterator", [new("value", value)]);
            LogThisRuntimeLogger.Entry(context);
            LogThisRuntimeLogger.Exit(context, value, null);
            Check(sink.Events.Count == 2 && Equals(sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value, "\"[Not enumerated]\""), "Iterator inspection prevented capture.");
            Check(Equals(sink.Events.First().Single(p => p.Key == "LogThisArguments").Value, "{\"value\":\"[Not enumerated]\"}"), "Iterator argument inspection occurred.");
            LogThisRuntimeLogger.Exit(context, new object[] { value }, null);
            Check(Equals(sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value, "[\"[Not enumerated]\"]"), "Nested iterator was inspected.");
        }
    }

    /// <summary>Fails the regression suite when an iterator invariant changes.</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    #endregion
}

/// <summary>Tracks body work independently of logging and supplies an identity-stable failure.</summary>
public sealed class IteratorState
{
    #region Public Properties
    /// <summary>Number of items whose production has started.</summary>
    public int Reads { get; set; }
    /// <summary>Number of executions of the iterator's finally block.</summary>
    public int Disposals { get; set; }
    /// <summary>Optional exception thrown when the second item is requested.</summary>
    public Exception? Failure { get; set; }
    /// <summary>Configuration visible inside the deferred body.</summary>
    public string? ProfileSeen { get; set; }
    #endregion
}

/// <summary>All six supported C# yield iterator signatures.</summary>
public interface IProductionIteratorSample
{
    IEnumerable<int> Sequence(IteratorState state);
    IEnumerator<int> Iterator(IteratorState state);
    IEnumerable LegacySequence(IteratorState state);
    IEnumerator LegacyIterator(IteratorState state);
    IAsyncEnumerable<int> AsyncSequence(IteratorState state, CancellationToken token = default);
    IAsyncEnumerator<int> AsyncIterator(IteratorState state, CancellationToken token = default);
}

/// <summary>Exclusion, masking, and ordinary collection behavior alongside yield templates.</summary>
[LogThis(Profile = "Iterator")]
public sealed class IteratorCaptureSample
{
    #region Public Methods
    /// <summary>A whole-return mask must avoid inspecting the sequence.</summary>
    [MaskReturnInLogs]
    public IEnumerable<int> Masked([DontLogThis] IteratorState state) { state.Reads++; yield return 1; }
    /// <summary>Explicit masks also replace async enumerators without advancing them.</summary>
    [MaskReturnInLogs("[ITERATOR]")]
    public async IAsyncEnumerator<int> MaskedAsync([DontLogThis] IteratorState state) { await Task.Yield(); state.Reads++; yield return 1; }
    /// <summary>Excluded yield methods retain their normal execution and emit no events.</summary>
    [DontLogThis]
    public IEnumerable<int> Excluded(IteratorState state) { state.Reads++; yield return 1; }
    /// <summary>A non-yield collection method returns and captures its original materialized list.</summary>
    public IEnumerable<int> Materialized(List<int> values) => values;
    #endregion
}

/// <summary>Every enumeration member throws if logging attempts to use it.</summary>
public sealed class UnreadEnumerator : IEnumerator<int>
{
    #region Public Members
    /// <inheritdoc />
    public int Current => throw new Exception("Current inspected.");
    object IEnumerator.Current => Current;
    /// <inheritdoc />
    public bool MoveNext() => throw new Exception("Advanced.");
    /// <inheritdoc />
    public void Reset() => throw new Exception("Reset.");
    /// <inheritdoc />
    public void Dispose() => throw new Exception("Disposed.");
    #endregion
}

/// <summary>Async enumeration and Current must remain untouched during capture.</summary>
public sealed class UnreadAsyncIterator : IAsyncEnumerable<int>, IAsyncEnumerator<int>
{
    #region Public Members
    /// <inheritdoc />
    public int Current => throw new Exception("Async Current inspected.");
    /// <inheritdoc />
    public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => throw new Exception("Enumerator requested.");
    /// <inheritdoc />
    public ValueTask<bool> MoveNextAsync() => throw new Exception("Advanced asynchronously.");
    /// <inheritdoc />
    public ValueTask DisposeAsync() => throw new Exception("Disposed asynchronously.");
    #endregion
}

/// <summary>Exercises Method-level production instrumentation on each yield signature.</summary>
public sealed class MethodIteratorSample : IProductionIteratorSample
{
    #region Public Methods
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    [LogThis(Profile = "Iterator")]
    public IEnumerable<int> Sequence([DontLogThis] IteratorState state)
    {
        try
        {
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    [LogThis(Profile = "Iterator")]
    public IEnumerator<int> Iterator([DontLogThis] IteratorState state)
    {
        try
        {
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    [LogThis(Profile = "Iterator")]
    public IEnumerable LegacySequence([DontLogThis] IteratorState state)
    {
        try
        {
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    [LogThis(Profile = "Iterator")]
    public IEnumerator LegacyIterator([DontLogThis] IteratorState state)
    {
        try
        {
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    [LogThis(Profile = "Iterator")]
    public async IAsyncEnumerable<int> AsyncSequence([DontLogThis] IteratorState state, [EnumeratorCancellation] CancellationToken token = default)
    {
        try
        {
            await Task.Yield(); token.ThrowIfCancellationRequested();
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            token.ThrowIfCancellationRequested();
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    [LogThis(Profile = "Iterator")]
    public async IAsyncEnumerator<int> AsyncIterator([DontLogThis] IteratorState state, CancellationToken token = default)
    {
        try
        {
            await Task.Yield(); token.ThrowIfCancellationRequested();
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            token.ThrowIfCancellationRequested();
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    #endregion
}

/// <summary>Exercises Class-level production instrumentation on each yield signature.</summary>
[LogThis(Profile = "Iterator")]
public sealed class ClassIteratorSample : IProductionIteratorSample
{
    #region Public Methods
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    public IEnumerable<int> Sequence([DontLogThis] IteratorState state)
    {
        try
        {
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    public IEnumerator<int> Iterator([DontLogThis] IteratorState state)
    {
        try
        {
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    public IEnumerable LegacySequence([DontLogThis] IteratorState state)
    {
        try
        {
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    public IEnumerator LegacyIterator([DontLogThis] IteratorState state)
    {
        try
        {
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    public async IAsyncEnumerable<int> AsyncSequence([DontLogThis] IteratorState state, [EnumeratorCancellation] CancellationToken token = default)
    {
        try
        {
            await Task.Yield(); token.ThrowIfCancellationRequested();
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            token.ThrowIfCancellationRequested();
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    /// <summary>Produces two deferred items and records disposal, with an optional later failure.</summary>
    public async IAsyncEnumerator<int> AsyncIterator([DontLogThis] IteratorState state, CancellationToken token = default)
    {
        try
        {
            await Task.Yield(); token.ThrowIfCancellationRequested();
            state.ProfileSeen = LogThisRuntimeContext.Current?.Configuration.JsonMaskValue;
            state.Reads++;
            yield return 1;
            token.ThrowIfCancellationRequested();
            if (state.Failure != null) throw state.Failure;
            state.Reads++;
            yield return 2;
        }
        finally { state.Disposals++; }
    }
    #endregion
}
