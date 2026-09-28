using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class NamedArgumentChecks
{
    public static async Task RunAsync()
    {
        var sink = new RecordingLogger();
        var options = new LogThisConfiguration { LogMethodArguments = true };
        options.JsonFieldsToMask.Add("Masked");
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(sink));
        services.AddLogThisConfiguration(options);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var sample = new NamedSample();

        sample.Authenticate("alice", "password value", true);
        Expect(sink, "Entered | Method: Authenticate | Arguments: {\"username\":\"alice\",\"rememberMe\":true}", "Exited | Method: Authenticate");
        sample.Alignment("first secret", "z-value", "middle secret", null, "a-value", "last secret");
        Expect(sink, "Entered | Method: Alignment | Arguments: {\"zebra\":\"z-value\",\"nullable\":null,\"alpha\":\"a-value\"}", "Exited | Method: Alignment");
        sample.Empty();
        Expect(sink, "Entered | Method: Empty | Arguments: {}", "Exited | Method: Empty");

        var payload = new CapturePayload();
        sample.Nested(new[] { payload });
        Expect(sink, "Entered | Method: Nested | Arguments: {\"items\":[{\"Visible\":\"included\",\"Masked\":\"*****\"}]}", "Exited | Method: Nested");
        if (payload.SecretReads != 0 || payload.StoredSecret != "excluded property value")
            throw new Exception("Property exclusion invoked a getter or mutated the payload.");
        // Parameter names do not extend JsonFieldsToMask, which still applies inside values.
        sample.MaskingScope("unchanged scalar");
        Expect(sink, "Entered | Method: MaskingScope | Arguments: {\"Masked\":\"unchanged scalar\"}", "Exited | Method: MaskingScope");

        var failure = new InvalidOperationException("original");
        try { await sample.FailAsync("alice", "password", false, failure); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Expect(sink, "Entered | Method: FailAsync | Arguments: {\"username\":\"alice\",\"rememberMe\":false}", "Exception | Method: FailAsync | Arguments: {\"username\":\"alice\",\"rememberMe\":false}");

        var legacy = new LogThisMethodContext("Legacy", "Manual", ["legacy", 3]);
        LogThisRuntimeLogger.Entry(legacy);
        LogThisRuntimeLogger.Exception(legacy, failure);
        Expect(sink, "Entered | Method: Legacy | Arguments: [\"legacy\", 3]", "Exception | Method: Legacy | Arguments: [\"legacy\", 3]");
        if (legacy.NamedArguments != null || legacy.Arguments.Length != 2)
            throw new Exception("Legacy context API changed.");
        var named = LogThisMethodContext.FromNamedArguments("Named", "Manual", [new("answer", 42)]);
        if (!Equals(named.Arguments[0], 42) || named.NamedArguments?[0].Key != "answer")
            throw new Exception("Named context values view is incorrect.");

        options.LogMethodArguments = false;
        var unread = new ThrowingCapture();
        try { sample.FailWithValue(unread, failure); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Expect(sink, "Entered | Method: FailWithValue", "Exception | Method: FailWithValue");
        if (unread.Reads != 0) throw new Exception("Disabled argument capture serialized a value.");
    }

    private static void Expect(RecordingLogger sink, params string[] expected)
    {
        if (!sink.Messages.SequenceEqual(expected))
            throw new Exception($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", sink.Messages)}]");
        sink.Messages.Clear();
    }
}

[LogThis]
public sealed class NamedSample
{
    public void Authenticate(string username, [DontLogThis] string password, bool rememberMe) { }
    public void Alignment([DontLogThis] string first, string zebra, [DontLogThis] string middle,
        string? nullable, string alpha, [DontLogThis] string last) { }
    public void Empty() { }
    public void Nested(CapturePayload[] items) { }
    public void MaskingScope(string Masked) { }
    public async Task FailAsync(string username, [DontLogThis] string password, bool rememberMe,
        [DontLogThis] Exception failure)
    {
        await Task.Yield();
        throw failure;
    }
    public void FailWithValue(object value, [DontLogThis] Exception failure) => throw failure;
}
