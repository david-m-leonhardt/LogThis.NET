using System.Collections;
using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

/// <summary>Checks parameter masking through real woven entry and exception events.</summary>
public static class ParameterMaskChecks
{
    #region Public Methods

    /// <summary>Verifies replacement, isolation, exclusions, and unchanged application behavior.</summary>
    public static async Task RunAsync()
    {
        var sink = new CorrelationLogger();
        var options = new LogThisConfiguration { LogMethodArguments = true, LogMethodReturnValue = false, JsonMaskValue = "default-mask" };
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(options);
        services.AddLogThisProfile("ParameterMask", o => { o.LogMethodArguments = true; o.JsonMaskValue = "profile-mask"; });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var sample = new ParameterMaskSample();
        var unread = new ThrowingCapture();
        var collection = new UnreadSequence();
        var failure = new InvalidOperationException("original");
        Check(ReferenceEquals(sample.Capture("alice", unread, collection, null, unread, "actual"), unread), "Application parameter identity changed.");

        Verify(sink.Events.First(), "default-mask");
        Check(unread.Reads == 0 && collection.Reads == 0, "Masked parameter contents were inspected.");
        sink.Events.Clear();
        try { await sample.Fail("alice", unread, collection, null, unread, "actual", failure); throw new Exception("Exception swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(sink.Events.Count == 2, "Masked exception capture lost events.");
        foreach (var entry in sink.Events) Verify(entry, "profile-mask");
        Check(unread.Reads == 0 && collection.Reads == 0, "Exception capture inspected masked contents.");
        sink.Events.Clear();
        options.LogMethodReturnValue = true;
        Check(sample.Forward("original") == "original", "Return value changed.");
        var entries = sink.Events.ToArray();
        Check((string?)Arguments(entries[0])["value"] == "[PARENT]", "Parent parameter mask missing.");
        Check((string?)Arguments(entries[1])["value"] == "original", "Parameter mask followed value into child call.");
        Check(entries.Where(e => e.Any(p => p.Key == "LogThisReturnValue")).All(e => Equals(e.Single(p => p.Key == "LogThisReturnValue").Value, "\"original\"")), "Parameter mask followed return value.");
        sink.Events.Clear();
        var payload = new CapturePayload();
        sample.Property(payload);
        Check((string?)Arguments(sink.Events.First())["value"]!["Visible"] == "included" && Arguments(sink.Events.First())["value"]!["StoredSecret"] == null, "Property exclusion changed.");
        sink.Events.Clear();
        options.LogMethodArguments = false;
        options.LogMethodReturnValue = false;
        sample.Capture("alice", unread, collection, null, unread, "actual");
        Check(sink.Events.Count == 2 && sink.Events.All(e => e.All(p => p.Key != "LogThisArguments")), "Disabled capture emitted arguments.");
        options.OnEntryConfig.LogAccessPoint = options.OnExitConfig.LogAccessPoint = false;
        sink.Events.Clear();
        sample.Capture("alice", unread, collection, null, unread, "actual");
        Check(sink.Events.IsEmpty && unread.Reads == 0 && collection.Reads == 0, "Disabled events inspected data.");
        object reference = unread;
        sample.ByReference(ref reference, out object output);
        Check(ReferenceEquals(reference, unread) && ReferenceEquals(output, unread), "Masked ref/out arguments changed execution.");
    }

    #endregion

    #region Private Methods

    private static JObject Arguments(KeyValuePair<string, object?>[] entry) => JObject.Parse((string)entry.Single(p => p.Key == "LogThisArguments").Value!);
    private static void Verify(KeyValuePair<string, object?>[] entry, string mask)
    {
        var args = Arguments(entry);
        Check(args.Properties().Select(p => p.Name).SequenceEqual(new[] { "username", "value", "items", "nullable", "empty" }), "Parameter names/order/exclusion changed.");
        Check((string?)args["username"] == "alice" && (string?)args["value"] == mask && (string?)args["items"] == "[TOKEN]" && (string?)args["nullable"] == mask && (string?)args["empty"] == "", "Parameter replacement mismatch.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    #endregion
}

/// <summary>Actual parameter values remain available to application code.</summary>
[LogThis]
public sealed class ParameterMaskSample
{
    #region Public Methods

    public object Capture(string username, [MaskThisInLogs] object value, [MaskThisInLogs("[TOKEN]")] IEnumerable items,
        [MaskThisInLogs] object? nullable, [DontLogThis, MaskThisInLogs] object excluded, [MaskThisInLogs("")] string empty) => value;
    [LogThis(Profile = "ParameterMask")]
    public async Task Fail(string username, [MaskThisInLogs] object value, [MaskThisInLogs("[TOKEN]")] IEnumerable items,
        [MaskThisInLogs] object? nullable, [DontLogThis, MaskThisInLogs] object excluded, [MaskThisInLogs("")] string empty,
        [DontLogThis] Exception failure) { await Task.Yield(); throw failure; }
    public string Forward([MaskThisInLogs("[PARENT]")] string value) => Child(value);
    public string Child(string value) => value;
    public void Property(CapturePayload value) { }
    public void ByReference([MaskThisInLogs] ref object value, [MaskThisInLogs] out object output) => output = value;

    #endregion
}

/// <summary>A collection whose contents must never be requested for masked parameters.</summary>
public sealed class UnreadSequence : IEnumerable
{
    #region Public Properties

    public int Reads { get; private set; }

    #endregion

    #region Public Methods

    public IEnumerator GetEnumerator() { Reads++; throw new Exception("Masked collection enumerated."); }

    #endregion
}
