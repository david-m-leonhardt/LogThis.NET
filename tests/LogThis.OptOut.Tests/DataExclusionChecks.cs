using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public static class DataExclusionChecks
{
    public static async Task RunAsync()
    {
        var sink = new RecordingLogger();
        var options = new LogThisConfiguration { LogMethodArguments = true, LogMethodReturnValue = true, LogCorrelationId = false };
        options.JsonFieldsToMask.Add("Masked");
        options.JsonMaskValue = "REDACTED";
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(sink));
        services.AddLogThisConfiguration(options);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var sample = new DataSample(sink);
        var secret = new ThrowingCapture();

        Check(sample.Mixed("visible", secret, 7) == 7, "Parameter exclusion changed the result.");
        Expect(sink, "Entered | Method: Mixed | Arguments: {\"visible\":\"visible\",\"number\":7}", "Exited | Method: Mixed | Return value: 7");
        sample.AllExcluded(secret);
        Expect(sink, "Entered | Method: AllExcluded | Arguments: {}", "Exited | Method: AllExcluded | Return value: null");
        Check(await sample.Async("visible", secret) == 8, "Async result changed.");
        Expect(sink, "Entered | Method: Async | Arguments: {\"visible\":\"visible\"}", "Exited | Method: Async | Return value: 8");
        Check(await sample.ValueTask(secret) == 9, "ValueTask result changed.");
        Expect(sink, "Entered | Method: ValueTask | Arguments: {}", "Exited | Method: ValueTask | Return value: 9");
        sample.Generic(secret, "generic");
        Expect(sink, "Entered | Method: Generic | Arguments: {\"visible\":\"generic\"}", "Exited | Method: Generic | Return value: null");
        Check(secret.Reads == 0, "Excluded parameter was serialized.");
        new DirectDataSample().Capture("direct", secret);
        Expect(sink, "Entered | Method: Capture | Arguments: {\"visible\":\"direct\"}", "Exited | Method: Capture | Return value: null");
        object refSecret = secret;
        sample.ByReference(ref refSecret, out object outSecret);
        Expect(sink, "Entered | Method: ByReference | Arguments: {}", "Exited | Method: ByReference | Return value: null");
        Check(ReferenceEquals(refSecret, secret) && ReferenceEquals(outSecret, secret), "Reference parameter behavior changed.");

        var payload = new CapturePayload();
        var graph = new { Direct = payload, Items = new object?[] { payload, null }, Map = new Dictionary<string, object> { ["item"] = payload } };
        Check(ReferenceEquals(sample.RoundTrip(graph), graph), "Return object identity changed.");
        Check(sink.Messages.Count == 2, "Property serialization suppressed an event.");
        foreach (string message in sink.Messages)
        {
            Check(message.Contains("\"Direct\":") && message.Contains("\"Items\":") && message.Contains("\"Map\":"), "Nested structure was lost.");
            Check(message.Contains("\"Visible\":\"included\""), "Included property missing.");
            Check(message.Contains("\"Masked\":\"REDACTED\""), "Existing masking was not applied.");
            Check(!message.Contains("Secret") && !message.Contains("renamed-secret") && !message.Contains("NullSecret"), "Excluded property name leaked.");
            Check(!message.Contains("original masked value"), "Unmasked value leaked.");
            Check(!message.Contains("excluded property value"), "Excluded property value leaked.");
        }
        sink.Messages.Clear();
        Check(payload.SecretReads == 0, "An excluded getter ran.");
        Check(payload.Masked == "original masked value", "Masking mutated the application object.");
        Check(payload.StoredSecret == "excluded property value", "Exclusion mutated the application object.");
        Check(ReferenceEquals(await sample.RoundTripAsync(payload), payload), "Async object identity changed.");
        Check(sink.Messages.Count == 2 && sink.Messages.All(message => message.Contains("\"Visible\":\"included\"") && !message.Contains("renamed-secret")), "Async property capture was incorrect.");
        sink.Messages.Clear();

        // An excluded parameter can still be returned or passed to a separately instrumented method.
        Check(ReferenceEquals(sample.ReturnExcluded(payload), payload), "Excluded input changed the return value.");
        Check(sink.Messages[0] == "Entered | Method: ReturnExcluded | Arguments: {}", "Excluded input captured.");
        Check(sink.Messages[1].Contains("\"Visible\":\"included\"") && !sink.Messages[1].Contains("Secret"), "Return capture did not independently honor property exclusion.");
        sink.Messages.Clear();
        sample.ForwardExcluded(payload);
        Check(sink.Messages.Count == 4 && sink.Messages[1].StartsWith("Entered | Method: RoundTrip | Arguments: {"), "Called method was suppressed.");
        Check(sink.Messages[1].Contains("\"Visible\":\"included\"") && !sink.Messages[1].Contains("Secret"), "Called method capture was incorrect.");
        sink.Messages.Clear();

        var failure = new InvalidOperationException("exception text remains unchanged");
        try { sample.Fail("visible", secret, failure); throw new Exception("Exception swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Expect(sink, "Entered | Method: Fail | Arguments: {\"visible\":\"visible\"}", "Exception | Method: Fail | Arguments: {\"visible\":\"visible\"} | Return value: ");
        try { await sample.FailAsync("visible", secret, failure); throw new Exception("Async exception swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Expect(sink, "Entered | Method: FailAsync | Arguments: {\"visible\":\"visible\"}", "Exception | Method: FailAsync | Arguments: {\"visible\":\"visible\"} | Return value: ");
        Check(failure.Message == "exception text remains unchanged", "Exception text changed.");
        try { sample.FailWithPayload(payload, failure); throw new Exception("Exception swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(sink.Messages.Count == 2 && sink.Messages[1].StartsWith("Exception | Method: FailWithPayload | Arguments:"), "Exception event missing.");
        Check(sink.Messages.All(message => message.Contains("\"Visible\":\"included\"") && !message.Contains("renamed-secret")), "Exception argument property exclusion failed.");
        sink.Messages.Clear();

        options.LogMethodArguments = false;
        sample.RoundTrip(payload);
        Check(sink.Messages.Count == 2 && sink.Messages[0] == "Entered | Method: RoundTrip" && sink.Messages[1].Contains("\"Visible\":\"included\"") && !sink.Messages[1].Contains("renamed-secret"), "Return-only capture failed.");
        sink.Messages.Clear();
        options.LogMethodArguments = true;
        options.LogMethodReturnValue = false;
        sample.RoundTrip(payload);
        Check(sink.Messages.Count == 2 && sink.Messages[0].Contains("\"Visible\":\"included\"") && !sink.Messages[0].Contains("renamed-secret") && sink.Messages[1] == "Exited | Method: RoundTrip", "Argument-only capture failed.");
        sink.Messages.Clear();

        // Runtime configuration remains independent of declarations and can change after weaving.
        options.LogMethodArguments = false;
        options.LogMethodReturnValue = false;
        Check(ReferenceEquals(sample.RoundTrip(secret), secret), "Disabled capture changed the result.");
        Expect(sink, "Entered | Method: RoundTrip", "Exited | Method: RoundTrip");
        sample.AllExcluded(secret);
        Expect(sink, "Entered | Method: AllExcluded", "Exited | Method: AllExcluded");
        Check(secret.Reads == 0, "Disabled capture invoked a getter.");

        options.LogMethodArguments = true;
        options.LogMethodReturnValue = true;
        options.OnEntryConfig.LogAccessPoint = false;
        options.OnExitConfig.LogAccessPoint = false;
        sample.RoundTrip(secret);
        Expect(sink);
        Check(secret.Reads == 0, "Disabled events invoked a getter.");
        try { sample.Fail("visible", secret, failure); throw new Exception("Exception swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Expect(sink, "Exception | Method: Fail | Arguments: {\"visible\":\"visible\"} | Return value: ");
        options.OnExceptionConfig.LogAccessPoint = false;
        try { sample.Fail("visible", secret, failure); throw new Exception("Exception swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Expect(sink);
        options.OnEntryConfig.LogAccessPoint = true;
        options.OnExitConfig.LogAccessPoint = true;
        sample.Explicit("explicit secret");
        Expect(sink, "Entered | Method: Explicit | Arguments: {}", "explicit secret", "Exited | Method: Explicit | Return value: null");
        Check(payload.SecretReads == 0 && secret.Reads == 0, "Excluded getter ran during a later capture.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Expect(RecordingLogger sink, params string[] expected)
    {
        Check(sink.Messages.SequenceEqual(expected), $"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", sink.Messages)}]");
        sink.Messages.Clear();
    }
}

[LogThis]
public sealed class DataSample(ILogger logger)
{
    public int Mixed(string visible, [DontLogThis] object secret, int number) => number;
    public void AllExcluded([DontLogThis] object secret) { }
    public async Task<int> Async(string visible, [DontLogThis] object secret) { await Task.Yield(); return 8; }
    public async ValueTask<int> ValueTask([DontLogThis] object secret) { await Task.Yield(); return 9; }
    public void Generic<T>([DontLogThis] T secret, string visible) { }
    public void ByReference([DontLogThis] ref object secret, [DontLogThis] out object output) => output = secret;
    public object RoundTrip(object value) => value;
    public async Task<object> RoundTripAsync(object value) { await Task.Yield(); return value; }
    public object ReturnExcluded([DontLogThis] object value) => value;
    public void ForwardExcluded([DontLogThis] object value) => RoundTrip(value);
    public void Fail(string visible, [DontLogThis] object secret, [DontLogThis] Exception failure) => throw failure;
    public void FailWithPayload(CapturePayload value, [DontLogThis] Exception failure) => throw failure;
    public async Task FailAsync(string visible, [DontLogThis] object secret, [DontLogThis] Exception failure)
    {
        await Task.Yield();
        throw failure;
    }
    public void Explicit([DontLogThis] string secret) => logger.LogInformation("{Value}", secret);
}

public sealed class DirectDataSample
{
    [LogThis]
    public void Capture(string visible, [DontLogThis] object secret) { }
}

public sealed class CapturePayload
{
    [JsonIgnore]
    public int SecretReads { get; private set; }
    public string Visible => "included";
    public string Masked { get; set; } = "original masked value";
    [DontLogThis]
    public string StoredSecret { get; set; } = "excluded property value";
    [DontLogThis, JsonProperty("renamed-secret")]
    public string Secret { get { SecretReads++; throw new Exception("Excluded getter must never run."); } }
    [DontLogThis]
    public string? NullSecret => null;
}

public sealed class ThrowingCapture
{
    [JsonIgnore]
    public int Reads { get; private set; }
    public string Value { get { Reads++; throw new Exception("This object must not be serialized."); } }
}
