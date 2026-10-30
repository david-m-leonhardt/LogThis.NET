using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Checks pre-access masks and the dictionary/JSON fallback through real woven methods.</summary>
public static class MaskingChecks
{
    #region Public Methods

    /// <summary>Checks precedence, application outcomes, disabled capture, and configuration isolation.</summary>
    public static async Task RunAsync()
    {
        try { _ = new MaskThisInLogsAttribute(null!); throw new Exception("Null replacement accepted."); }
        catch (ArgumentNullException exception) when (exception.ParamName == "replacement") { }
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(MaskThisInLogsAttribute), typeof(AttributeUsageAttribute))!;
        Check(usage.ValidOn == (AttributeTargets.Property | AttributeTargets.Parameter) && !usage.Inherited, "Mask attribute targets or inheritance changed.");
        var payload = new MaskPayload();
        var sample = new MaskSample();
        var sink = new CorrelationLogger();
        var options = Options("configured");
        using var provider = Provider(options, sink);
        using (provider.UseLogThis())
        {
            object graph = new { Item = payload, Items = new[] { payload } };
            Check(ReferenceEquals(await sample.Echo(graph), graph), "Returned object changed.");
            Check(sink.Events.Count == 2, "Capture failed or changed event count.");
            foreach (var entry in sink.Events)
            {
                var field = entry.Single(p => p.Key is "LogThisArguments" or "LogThisReturnValue");
                JToken captured = JToken.Parse((string)field.Value!);
                if (field.Key == "LogThisArguments") captured = captured["value"]!;
                Verify(captured["Item"]!, "configured");
                Check((string?)captured["Items"] == "[Not read]", "Enumerable property was not skipped.");
            }
            Check(payload.Reads == 0 && payload.Password == "real password" && payload.Null == null, "Capture read a masked getter or changed the model.");
            Check(payload.StoredCustomer.Secret == "customer secret" && payload.StoredItems.Length == 1, "Whole-value masking changed contents.");
            sink.Events.Clear();
            var failure = new InvalidOperationException("original exception message");
            try { sample.Fail(payload, failure); throw new Exception("Exception swallowed."); }
            catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
            Check(sink.Events.Count == 2 && sink.Events.Last().Any(p => p.Key == "LogThisOnException"), "Masked exception capture failed.");
            Verify(JToken.Parse((string)sink.Events.Last().Single(p => p.Key == "LogThisArguments").Value!)["value"]!, "configured");
            Check(failure.Message == "original exception message", "Exception was modified.");

            // Retain tree masking for representations that do not expose ordinary property contracts.
            var json = JObject.Parse("{\"PASSWORD\":\"json secret\",\"Nested\":[{\"Password\":null}]}");
            var dictionary = new Dictionary<string, object?> { ["Password"] = new { Secret = "dictionary secret" }, ["Json"] = json, ["Model"] = payload };
            JObject capturedDictionary = JObject.Parse(LogThisValueMasker.MaskObject(dictionary));
            Check((string?)capturedDictionary["Password"] == "configured" && (string?)capturedDictionary["Json"]!["PASSWORD"] == "configured" && (string?)capturedDictionary["Json"]!["Nested"]![0]!["Password"] == "configured", "Dictionary/JSON fallback lost coverage.");
            Verify(capturedDictionary["Model"]!, "configured");
            Check((string?)json["PASSWORD"] == "json secret" && dictionary["Password"] is not string, "Fallback mutated original values.");

            using var otherProvider = Provider(Options("other"), new CorrelationLogger());
            using (otherProvider.UseLogThis()) Verify(JToken.Parse(LogThisValueMasker.MaskObject(payload)), "other");
            Verify(JToken.Parse(LogThisValueMasker.MaskObject(payload)), "configured");
            options.JsonMaskValue = "changed";
            Verify(JToken.Parse(LogThisValueMasker.MaskObject(payload)), "changed");
            options.JsonFieldsToMask.Clear();
            var ordinary = new { Password = "visible" };
            Check((string?)JObject.Parse(LogThisValueMasker.MaskObject(ordinary))["Password"] == "visible", "Cached name rule leaked.");
            options.JsonFieldsToMask.Add("password");
            Check((string?)JObject.Parse(LogThisValueMasker.MaskObject(ordinary))["Password"] == "changed", "Updated name rule was ignored.");
            options.LogMethodArguments = false;
            options.LogMethodReturnValue = false;
            sink.Events.Clear();
            var unread = new UnreadMaskedPayload();
            Check(ReferenceEquals(await sample.Echo(unread), unread), "Disabled capture changed result.");
            Check(sink.Events.Count == 2 && sink.Events.All(e => e.All(p => p.Key is not "LogThisArguments" and not "LogThisReturnValue")), "Disabled capture emitted values.");
        }
        // Independent providers overlap to exercise resolver/configuration isolation across async flows.
        async Task Capture(string mask)
        {
            using var independent = Provider(Options(mask), new CorrelationLogger());
            using var scope = independent.UseLogThis();
            await Task.Yield();
            Verify(JToken.Parse(LogThisValueMasker.MaskObject(payload)), mask);
        }
        await Task.WhenAll(Capture("first"), Capture("second"));
        await Capture(new LogThisConfiguration().JsonMaskValue);
        Check(payload.Reads == 0, "A masked getter ran during a later capture.");
    }

    #endregion

    #region Private Methods

    /// <summary>Creates per-provider configuration with case-insensitive serialized-name matches.</summary>
    private static LogThisConfiguration Options(string mask) => new()
    {
        LogMethodArguments = true, LogMethodReturnValue = true, LogCorrelationId = false,
        JsonMaskValue = mask, JsonFieldsToMask = ["password", "renamed", "Explicit", "NullConfigured", "Empty", "Excluded"]
    };

    /// <summary>Creates an independent runtime provider.</summary>
    private static ServiceProvider Provider(LogThisConfiguration options, CorrelationLogger sink)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(options);
        return services.BuildServiceProvider();
    }

    /// <summary>Checks whole replacements, omission, null inclusion, and explicit precedence.</summary>
    private static void Verify(JToken token, string mask)
    {
        Check((string?)token["Password"] == mask && (string?)token["renamed"] == mask, "Configured mask or renamed property matching failed.");
        Check((string?)token["Explicit"] == "[PRIVATE]" && (string?)token["Customer"] == "[CUSTOMER]", "Explicit replacement overwritten or traversed.");
        Check((string?)token["Items"] == mask && (string?)token["Null"] == mask && (string?)token["NullConfigured"] == mask, "Collection/null mask omitted.");
        Check((string?)token["Empty"] == "" && token["Excluded"] == null, "Empty replacement or exclusion precedence failed.");
    }

    /// <summary>Reports a failed integration expectation.</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    #endregion

}

/// <summary>Contains getters that must never execute during capture.</summary>
public sealed class MaskPayload
{
    #region Public Properties

    [JsonIgnore] public int Reads { get; private set; }
    [JsonIgnore] public CustomerDetails StoredCustomer { get; } = new();
    [JsonIgnore] public CustomerDetails[] StoredItems { get; } = [new()];
    [MaskThisInLogs] public string Password { get; init; } = "real password";
    [JsonProperty("renamed")] public string Configured { get { Reads++; throw new Exception("Configured getter read."); } }
    [MaskThisInLogs("[PRIVATE]")] public object Explicit { get { Reads++; throw new Exception("Explicit getter read."); } }
    [MaskThisInLogs("[CUSTOMER]")] public CustomerDetails Customer { get { Reads++; return StoredCustomer; } }
    [MaskThisInLogs] public CustomerDetails[] Items { get { Reads++; return StoredItems; } }
    [MaskThisInLogs, JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string? Null { get; init; }
    public string? NullConfigured { get { Reads++; return null; } }
    [MaskThisInLogs(""), JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public string Empty => throw new Exception("Empty getter read.");
    [DontLogThis, MaskThisInLogs("excluded")] public string Excluded => throw new Exception("Excluded getter read.");

    #endregion

    #region Public Methods

    /// <summary>A conditional serialization hook must not access masked data either.</summary>
    public bool ShouldSerializeExplicit() { Reads++; throw new Exception("Masked serialization hook ran."); }

    #endregion
}

/// <summary>Replacement must prevent traversal of the containing property.</summary>
public sealed class CustomerDetails
{
    #region Public Properties

    public string Secret { get; init; } = "customer secret";
    public string Throwing => throw new Exception("Masked contents traversed.");

    #endregion
}

/// <summary>Failure during contract creation establishes that disabled capture does not inspect attributes.</summary>
public sealed class UnreadMaskedPayload
{
    #region Public Properties

    [MaskThisInLogs(null!)] public string Secret => throw new Exception("Disabled getter read.");

    #endregion
}

/// <summary>Exercises masks through entry, awaited exit, and exception capture.</summary>
[LogThis]
public sealed class MaskSample
{
    #region Public Methods

    public async Task<object> Echo(object value) { await Task.Yield(); return value; }
    public void Fail(object value, [DontLogThis] Exception failure) => throw failure;

    #endregion
}
