using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Interfaces;
using LogThis.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Checks completed capture previews, configuration paths, and woven application outcomes.</summary>
public static class TruncationChecks
{
    #region Public Methods

    /// <summary>Runs formatting boundaries and profile-aware capture checks.</summary>
    public static void Run()
    {
        CheckFormatting();
        CheckConfiguration();
        CheckEvents();
    }

    #endregion

    #region Private Methods

    /// <summary>Verifies UTF-16 boundaries and Newtonsoft escaping without interpreting partial previews.</summary>
    private static void CheckFormatting()
    {
        var options = new LogThisConfiguration();
        Check(options.TruncateCapturedValues && options.MaxCapturedValueLength == 4096, "Defaults changed.");
        foreach (string text in new[] { "", "null", "\"a\\\"\\\\\\n😀z\"" })
        {
            for (int limit = 1; limit <= text.Length + 1; limit++)
            {
                options.MaxCapturedValueLength = limit;
                string output = LogThisValueMasker.FormatCapture(text, options);
                if (text.Length <= limit) Check(output == text, "Within-limit payload changed.");
                else
                {
                    int expectedLength = limit;
                    if (char.IsHighSurrogate(text[limit - 1]) && char.IsLowSurrogate(text[limit])) expectedLength--;
                    var wrapper = JObject.Parse(output);
                    Check(wrapper.Count == 2 && (bool)wrapper["truncated"]! && (string)wrapper["preview"]! == text[..expectedLength], "Preview or escaping changed.");
                }
                options.TruncateCapturedValues = false;
                Check(LogThisValueMasker.FormatCapture(text, options) == text, "Unlimited output changed.");
                options.TruncateCapturedValues = true;
            }
        }
        options.MaxCapturedValueLength = 1;
        Check((string)JObject.Parse(LogThisValueMasker.FormatCapture("😀", options))["preview"]! == "", "Small limit split a surrogate pair.");
    }

    /// <summary>Checks binding, callbacks, prepared instances, and positive-length validation for every profile.</summary>
    private static void CheckConfiguration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LogThis:MaxCapturedValueLength"] = "12",
            ["LogThis:TruncateCapturedValues"] = "false",
            ["LogThis:Profiles:Small:MaxCapturedValueLength"] = "3",
            ["LogThis:Profiles:Small:TruncateCapturedValues"] = "true"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogThisConfiguration(config.GetSection("LogThis"));
        services.AddLogThisProfile("Callback", o => o.MaxCapturedValueLength = 7);
        using var provider = services.BuildServiceProvider();
        var profiles = provider.GetRequiredService<LogThisProfiles>();
        Check(!profiles.Default.Configuration.TruncateCapturedValues && profiles.Default.Configuration.MaxCapturedValueLength == 12, "Default binding failed.");
        Check(profiles.Select("Small").Configuration.TruncateCapturedValues && profiles.Select("Small").Configuration.MaxCapturedValueLength == 3, "Named binding failed.");
        Check(profiles.Select("Callback").Configuration.MaxCapturedValueLength == 7, "Callback registration failed.");
        foreach (int length in new[] { 0, -1 })
        foreach (bool enabled in new[] { false, true })
        foreach (bool named in new[] { false, true })
        foreach (int path in new[] { 0, 1, 2 })
        {
            var invalid = new ServiceCollection();
            invalid.AddLogging();
            var options = new LogThisConfiguration { TruncateCapturedValues = enabled, MaxCapturedValueLength = length };
            if (path == 0)
            {
                if (named) invalid.AddLogThisProfile("Bad", options);
                else invalid.AddLogThisConfiguration(options);
            }
            else if (path == 1)
            {
                void Configure(LogThisConfiguration o) { o.MaxCapturedValueLength = length; o.TruncateCapturedValues = enabled; }
                if (named) invalid.AddLogThisProfile("Bad", Configure);
                else invalid.AddLogThisConfiguration(Configure);
            }
            else
            {
                string prefix = named ? "LogThis:Profiles:Bad:" : "LogThis:";
                var binding = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [prefix + "MaxCapturedValueLength"] = length.ToString(),
                    [prefix + "TruncateCapturedValues"] = enabled.ToString()
                }).Build();
                invalid.AddLogThisConfiguration(binding.GetSection("LogThis"));
            }
            using var rejected = invalid.BuildServiceProvider();
            try { rejected.GetRequiredService<ILogThisScopeFactory>(); throw new Exception("Invalid length accepted."); }
            catch (ArgumentOutOfRangeException e) when (e.ParamName == "MaxCapturedValueLength") { }
        }
    }

    /// <summary>Verifies one combined argument limit and an independent result limit after masking.</summary>
    private static void CheckEvents()
    {
        var sink = new CorrelationLogger();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(o =>
        {
            o.LogCorrelationId = false;
            o.LogMethodArguments = o.LogMethodReturnValue = true;
            o.MaxCapturedValueLength = 25;
            o.JsonFieldsToMask.Add("Password");
            o.MessageComponents["Custom"] = new string('x', 100);
        });
        services.AddLogThisProfile("Unlimited", o =>
        {
            o.LogMethodArguments = o.LogMethodReturnValue = true;
            o.TruncateCapturedValues = false;
            o.MaxCapturedValueLength = 1;
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var sample = new TruncationSample();
        var value = new { Password = "SECRET", Text = new string('a', 60) };
        Check(ReferenceEquals(sample.Echo(value, "second"), value), "Application identity changed.");
        var events = sink.Events.ToArray();
        string args = "{\"value\":{\"Password\":\"*****\",\"Text\":\"" + new string('a', 60) + "\"},\"other\":\"second\"}";
        string result = "{\"Password\":\"*****\",\"Text\":\"" + new string('a', 60) + "\"}";
        Check((string)JObject.Parse(Field(events[0], "LogThisArguments"))["preview"]! == args[..25], "Arguments were not masked and limited once as a combined payload.");
        Check((string)JObject.Parse(Field(events[1], "LogThisReturnValue"))["preview"]! == result[..25], "Return limit was not independent.");
        Check(Field(events[0], "Custom").Length == 100 && Field(events[0], "LogThisMethod") == "Echo", "Non-capture components truncated.");
        Check(value.Password == "SECRET", "Masking mutated input.");
        sink.Events.Clear();
        Check(ReferenceEquals(sample.Unlimited(value), value), "Profile result changed.");
        Check(Field(sink.Events.Last(), "LogThisReturnValue") == JsonConvert.SerializeObject(value), "Unlimited profile did not remain isolated.");
        sink.Events.Clear();
        sample.Echo(value, "second");
        Check(JObject.Parse(Field(sink.Events.Last(), "LogThisReturnValue"))["truncated"]!.Value<bool>(), "Outer configuration was not restored.");
        sink.Events.Clear();
        var failure = new InvalidOperationException(new string('e', 100));
        try { sample.Fail(value, failure); throw new Exception("Failure swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(sink.Events.Count == 2 && sink.Events.Last().Any(p => p.Key == "LogThisOnException"), "Exception event selection changed.");
        Check(JObject.Parse(Field(sink.Events.Last(), "LogThisArguments"))["truncated"]!.Value<bool>() && Field(sink.Events.Last(), "LogThisReturnValue") == "", "Exception capture changed.");
        Check(failure.Message.Length == 100, "Exception text changed.");
        sink.Events.Clear();
        var unread = new ThrowingCapture();
        Check(ReferenceEquals(sample.Masked(unread), unread) && unread.Reads == 0, "Truncating a return mask inspected the original result.");
        string maskedResult = Field(sink.Events.Last(), "LogThisReturnValue");
        Check((string)JObject.Parse(maskedResult)["preview"]! == "\"" + new string('m', 24), "Whole-return mask was not truncated after replacement.");
        var options = provider.GetRequiredService<ILogThisConfiguration>();
        options.LogMethodArguments = options.LogMethodReturnValue = false;
        sink.Events.Clear();
        sample.Echo(unread, "second");
        Check(unread.Reads == 0 && sink.Events.All(e => e.All(p => p.Key is not "LogThisArguments" and not "LogThisReturnValue")), "Truncation enabled disabled capture.");
    }

    /// <summary>Reads a structured string field from one emitted event.</summary>
    private static string Field(KeyValuePair<string, object?>[] entry, string name) => (string)entry.Single(p => p.Key == name).Value!;

    /// <summary>Fails the executable suite when an invariant does not hold.</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    #endregion
}

/// <summary>Woven calls preserve original objects and failures under capture formatting.</summary>
[LogThis]
public class TruncationSample
{
    #region Public Methods

    /// <summary>Returns the original object while capturing multiple named arguments.</summary>
    public object Echo(object value, string other) => value;

    /// <summary>Selects an unlimited profile for this call only.</summary>
    [LogThis(Profile = "Unlimited")]
    public object Unlimited(object value) => value;

    /// <summary>Throws the supplied application exception.</summary>
    public void Fail(object value, [DontLogThis] Exception failure) => throw failure;

    /// <summary>Replaces a result before the capture preview is formatted.</summary>
    [MaskReturnInLogs("mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm")]
    public object Masked([DontLogThis] object value) => value;

    #endregion
}
