using System.Collections;
using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

/// <summary>Exercises whole-result replacement through woven methods and selected profiles.</summary>
public static class ReturnMaskChecks
{
    #region Public Methods

    /// <summary>Verifies masks without inspecting results or altering application outcomes.</summary>
    public static async Task RunAsync()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(MaskReturnInLogsAttribute), typeof(AttributeUsageAttribute))!;
        Check(usage.ValidOn == AttributeTargets.Method && !usage.Inherited, "Return mask targets/inheritance changed.");
        try { _ = new MaskReturnInLogsAttribute(null!); throw new Exception("Null literal accepted."); }
        catch (ArgumentNullException) { }
        Check(new MaskReturnInLogsAttribute("").Replacement == "", "Empty literal rejected.");
        var sink = new CorrelationLogger();
        var options = new LogThisConfiguration { LogMethodReturnValue = true, JsonMaskValue = "default-mask" };
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Information).AddProvider(sink));
        services.AddLogThisConfiguration(options);
        services.AddLogThisProfile("ReturnMask", o => { o.LogMethodReturnValue = true; o.JsonMaskValue = "profile-mask"; });
        services.AddLogThisProfile("NoReturn", o => o.LogMethodReturnValue = false);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var sample = new ReturnMaskSample();
        var unread = new ThrowingCapture();
        var sequence = new UnreadSequence();
        Check(ReferenceEquals(sample.Default(unread), unread), "Result identity changed.");
        ExpectMask(sink, "default-mask");
        Check(sample.Default(null) == null, "Null result changed.");
        ExpectMask(sink, "default-mask");
        Check(ReferenceEquals(sample.Literal(sequence), sequence), "Collection identity changed.");
        ExpectMask(sink, "[TOKEN]\"\\");
        Check(sample.Empty() == "original", "Empty mask changed result.");
        ExpectMask(sink, "");
        var gate = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object?> pending = sample.Awaited(gate.Task);
        Check(sink.Events.Count == 1 && !pending.IsCompleted, "Async exit emitted before completion.");
        gate.SetResult(unread);
        Check(ReferenceEquals(await pending, unread), "Awaited identity changed.");
        ExpectMask(sink, "profile-mask");
        Check(await sample.ValueTaskResult(null) == null, "ValueTask null changed.");
        ExpectMask(sink, "default-mask");
        Check(ReferenceEquals(await sample.LiteralTask(unread), unread), "Task result identity changed.");
        ExpectMask(sink, "[ASYNC]");
        Check(unread.Reads == 0 && sequence.Reads == 0, "Masked result inspected.");
        var failure = new InvalidOperationException("original exception text");
        try { await sample.Fail(failure); throw new Exception("Failure swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(sink.Events.Count == 2 && sink.Events.Last().Any(p => p.Key == "LogThisOnException"), "Failure emitted a successful exit.");
        Check(Equals(sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value, ""), "Exception result was masked.");
        sink.Events.Clear();
        try { sample.FailSync(failure); throw new Exception("Synchronous failure swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(sink.Events.Count == 2 && sink.Events.Last().Any(p => p.Key == "LogThisOnException") &&
            Equals(sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value, ""), "Synchronous exception capture changed.");
        sink.Events.Clear();
        Check(ReferenceEquals(sample.Excluded(unread), unread) && sink.Events.IsEmpty, "Exclusion lost precedence.");
        Check(new ReturnMaskUninstrumented().Value() == "original" && sink.Events.IsEmpty, "Attribute enabled instrumentation.");
        Check(new ReturnMaskDerived().Value() == "derived", "Override changed.");
        ExpectMask(sink, "derived"); // Unannotated override retains ordinary JSON capture.
        Check(new ReturnMaskAnnotatedDerived().Value() == "derived", "Annotated override changed.");
        ExpectMask(sink, "[OVERRIDE]");
        sample.NoReturn(unread);
        Check(sink.Events.Count == 2 && sink.Events.All(e => e.All(p => p.Key != "LogThisReturnValue")), "Mask enabled profile-disabled capture.");
        sink.Events.Clear();
#if RETURN_MASK_DIAGNOSTICS
        var noResult = new ReturnMaskWarningProbe();
        noResult.VoidResult();
        await noResult.TaskResult();
        await noResult.ValueTaskResult();
        Check(sink.Events.Count == 6 && sink.Events.Where(e => e.Any(p => p.Key == "LogThisOnExit"))
            .All(e => Equals(e.Single(p => p.Key == "LogThisReturnValue").Value, "null")), "No-result masks changed logging.");
        sink.Events.Clear();
#endif
        options.LogMethodReturnValue = false;
        sample.Default(unread);
        Check(sink.Events.Count == 2 && sink.Events.All(e => e.All(p => p.Key != "LogThisReturnValue")), "Mask enabled disabled capture.");
        sink.Events.Clear();
        options.LogMethodReturnValue = true;
        options.OnExitConfig.LogAccessPoint = false;
        sample.Default(unread);
        Check(sink.Events.Count == 1, "Mask enabled disabled exit.");
        sink.Events.Clear();
        options.OnExitConfig.LogAccessPoint = true;
        options.OnExitConfig.LogLevel = LogLevel.Debug;
        sample.Default(unread);
        Check(sink.Events.Count == 1 && unread.Reads == 0, "Mask bypassed level filtering.");
    }

    #endregion

    #region Private Methods

    private static void ExpectMask(CorrelationLogger sink, string expected)
    {
        Check(sink.Events.Count == 2, "Expected entry and exit.");
        string json = (string)sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value!;
        Check((string?)JToken.Parse(json) == expected, "Return replacement mismatch.");
        sink.Events.Clear();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    #endregion
}

/// <summary>Class-level instrumentation exercises result masks on ordinary and async methods.</summary>
[LogThis]
public sealed class ReturnMaskSample
{
    #region Public Methods

    [MaskReturnInLogs] public object? Default(object? value) => value;
    [MaskReturnInLogs("[TOKEN]\"\\")] public IEnumerable Literal(IEnumerable value) => value;
    [MaskReturnInLogs("")] public string Empty() => "original";
    [LogThis(Profile = "ReturnMask"), MaskReturnInLogs]
    public async Task<object?> Awaited(Task<object?> task) => await task;
    [MaskReturnInLogs] public async ValueTask<object?> ValueTaskResult(object? value) { await Task.Yield(); return value; }
    [LogThis, MaskReturnInLogs("[ASYNC]")] public Task<object> LiteralTask(object value) => Task.FromResult(value);
    [MaskReturnInLogs] public async Task<string> Fail(Exception failure) { await Task.Yield(); throw failure; }
    [MaskReturnInLogs] public string FailSync(Exception failure) => throw failure;
    [LogThis(Profile = "NoReturn"), MaskReturnInLogs] public object NoReturn(object value) => value;
    [DontLogThis, MaskReturnInLogs] public object Excluded(object value) => value;

    #endregion
}

/// <summary>An annotation alone does not enable logging.</summary>
public class ReturnMaskUninstrumented
{
    #region Public Methods

    [MaskReturnInLogs] public virtual string Value() => "original";

    #endregion
}

/// <summary>Overrides must repeat return masking intent.</summary>
public sealed class ReturnMaskDerived : ReturnMaskUninstrumented
{
    #region Public Methods

    [LogThis] public override string Value() => "derived";

    #endregion
}

/// <summary>An explicit override annotation masks its own result.</summary>
public sealed class ReturnMaskAnnotatedDerived : ReturnMaskUninstrumented
{
    #region Public Methods

    [LogThis, MaskReturnInLogs("[OVERRIDE]")] public override string Value() => "derived";

    #endregion
}
