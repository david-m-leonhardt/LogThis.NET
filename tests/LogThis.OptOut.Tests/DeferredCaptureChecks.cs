using System.Collections;
using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Verifies capture guards through real woven argument, result, and exception events.</summary>
public static class DeferredCaptureChecks
{
    #region Public Methods

    /// <summary>Checks getter/sequence isolation, supported values, converter suppression, and application outcomes.</summary>
    public static void Run()
    {
        var sink = new CorrelationLogger();
        var options = new LogThisConfiguration
        {
            LogMethodArguments = true, LogMethodReturnValue = true, LogCorrelationId = false,
            JsonMaskValue = "configured", JsonFieldsToMask = ["Password"]
        };
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(sink));
        services.AddLogThisConfiguration(options);
        services.AddLogThisProfile("Deferred", o =>
        {
            o.LogMethodArguments = true; o.LogMethodReturnValue = true; o.JsonMaskValue = "profile";
            o.JsonFieldsToMask = ["Password"];
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var sample = new DeferredSample();
        var deferred = new DeferredSequence();
        Check(ReferenceEquals(sample.Echo(deferred), deferred), "Deferred result identity changed.");
        Check(sink.Events.Count == 2, "Deferred capture lost events.");
        foreach (var entry in sink.Events)
        {
            var captured = entry.Single(p => p.Key is "LogThisArguments" or "LogThisReturnValue");
            JToken token = JToken.Parse((string)captured.Value!);
            Check((string?)(captured.Key == "LogThisArguments" ? token["value"] : token) == "[Not enumerated]", "Direct sequence was not replaced.");
        }
        Check(deferred.Enumerations == 0 && deferred.CountReads == 0 && deferred.StringReads == 0, "Logging inspected a sequence.");
        Check(deferred.Cast<int>().SequenceEqual([1, 2]) && deferred.Enumerations == 1, "Application could not enumerate the original sequence.");

        var throwing = new ThrowingSequence();
        var model = new DeferredProperties();
        var graph = new object?[] { model, new List<object?> { throwing, model, null, "text" },
            new Dictionary<string, object?> { ["value"] = throwing, ["item"] = model, ["Password"] = throwing } };
        JArray json = (JArray)Capture(graph);
        VerifyProperties(json[0], "configured");
        VerifyProperties(json[1]![1]!, "configured");
        VerifyProperties(json[2]!["item"]!, "configured");
        Check((string?)json[1]![0] == "[Not enumerated]" && (string?)json[2]!["value"] == "[Not enumerated]", "Nested sequences were accessed.");
        Check((string?)json[2]!["Password"] == "configured", "Dictionary masking lost precedence over a marker.");
        Check(json[1]![2]!.Type == JTokenType.Null && (string?)json[1]![3] == "text", "Null/string handling changed.");
        Check(model.Reads == 0 && model.Hooks == 0 && throwing.Reads == 0, "Protected getters, hooks, or enumerators ran.");
        var extension = new DeferredExtensionData();
        Check((string?)Capture(extension)["Extra"] == "[Not read]" && extension.Reads == 0, "Extension data bypassed the property guard.");
        Check((string?)Capture(new MaskedExtensionData())["Extra"] == "[EXTENSION]", "Extension data lost mask precedence.");
        Check(Capture(new ExcludedExtensionData())["Extra"] == null, "Excluded extension data was captured.");

        IEnumerable availableArray = new[] { 1, 2 };
        IEnumerable availableList = new List<int> { 1, 2 };
        IEnumerable availableDictionary = new Dictionary<string, int> { ["one"] = 1 };
        Check(Capture(sample.EchoEnumerable(availableArray)) is JArray, "Array behind IEnumerable was blocked.");
        Check(Capture(sample.EchoEnumerable(availableList)) is JArray, "List behind IEnumerable was blocked.");
        Check((int?)Capture(sample.EchoEnumerable(availableDictionary))["one"] == 1, "Dictionary behind IEnumerable was blocked.");
        Check((string?)Capture(new CustomList()) == "[Not enumerated]", "List subclass was allowed.");
        Check((string?)Capture(new CustomDictionary()) == "[Not enumerated]", "Dictionary subclass was allowed.");

        var hidden = new ObjectProperty(throwing);
        Check((string?)Capture(hidden)["Value"] == "[Not enumerated]" && hidden.Reads == 1, "Object property did not follow ordinary getter semantics.");
        var hiddenList = new ObjectProperty(new List<int> { 1, 2 });
        Check(Capture(hiddenList)["Value"] is JArray && hiddenList.Reads == 1, "Available object-typed list was blocked.");

        JObject prebuilt = new() { ["Password"] = "secret", ["Items"] = new JArray(new JObject { ["Password"] = "nested" }) };
        JToken capturedJson = Capture(prebuilt);
        Check((string?)capturedJson["Password"] == "configured" && (string?)capturedJson["Items"]![0]!["Password"] == "configured", "Prebuilt JSON masking changed.");
        Check((string?)prebuilt["Password"] == "secret", "Prebuilt JSON was mutated.");
        Check((string?)Capture(new JArray(prebuilt))[0]!["Password"] == "configured", "Direct JArray masking changed.");

        sink.Events.Clear();
        sample.Profile(model);
        Check(sink.Events.Count == 2, "Profile capture failed.");
        VerifyProperties(JToken.Parse((string)sink.Events.Last().Single(p => p.Key == "LogThisReturnValue").Value!), "profile");
        VerifyProperties(Capture(model), "configured");

        // Fresh closed generic converter types distinguish construction from serialization use.
        Check((string?)Capture(new TypeConverted())["Value"] == "ordinary", "Type converter was used.");
        Check(ProbeConverter<TypeProbe>.Constructors == 0, "Type converter was constructed.");
        JToken converted = Capture(new ConverterProperties());
        Check((string?)converted["Value"] == "ordinary" && converted["Items"] is JArray, "Property/item converter changed output.");
        Check((string?)converted["Masked"] == "[PRIVATE]" && (string?)converted["Sequence"] == "[Not read]", "Internal converters were cleared.");
        Check(ProbeConverter<PropertyProbe>.Constructors > 0 && ProbeConverter<ItemProbe>.Constructors > 0, "Property/item constructor probes did not run.");
        Check((string?)Capture(new ContainerConverted())["Value"] == "ordinary", "Container item converter changed output.");
        Check(ProbeConverter<ContainerProbe>.Constructors > 0, "Container item constructor probe did not run.");
        VerifyUnused<TypeProbe>(); VerifyUnused<PropertyProbe>(); VerifyUnused<ItemProbe>(); VerifyUnused<ContainerProbe>();

        var failedCapture = new ConstructorFailure();
        sink.Events.Clear();
        Check(ReferenceEquals(sample.Echo(failedCapture), failedCapture), "Converter construction failure changed result.");
        Check(sink.Events.IsEmpty && ThrowingConstructor.Calls > 0, "Failed capture was retried or emitted.");
        var failure = new InvalidOperationException("original");
        try { sample.Fail(failedCapture, failure); throw new Exception("Application exception swallowed."); }
        catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
        Check(sink.Events.IsEmpty && failedCapture.Reads == 0, "Constructor failure exposed a fallback capture.");

        options.LogMethodArguments = false;
        options.LogMethodReturnValue = false;
        sink.Events.Clear();
        sample.Echo(model);
        Check(sink.Events.Count == 2 && sink.Events.All(e => e.All(p => p.Key is not "LogThisArguments" and not "LogThisReturnValue")), "Disabled capture was enabled.");
        Check(model.Reads == 0 && model.Hooks == 0, "Disabled capture read a property.");

        // Ordinary Json.NET serialization still uses the application converter.
        JsonConvert.SerializeObject(new TypeConverted());
        Check(ProbeConverter<TypeProbe>.Constructors > 0 && ProbeConverter<TypeProbe>.Writes > 0, "Capture altered serialization outside LogThis.");
    }

    #endregion

    #region Private Methods

    private static JToken Capture(object? value) => JToken.Parse(LogThisValueMasker.MaskObject(value));
    private static void VerifyProperties(JToken value, string mask)
    {
        foreach (string name in new[] { "Array", "List", "Map", "Sequence", "NullArray", "Json" })
            Check((string?)value[name] == "[Not read]", "Enumerable property was read: " + name);
        Check((string?)value["Password"] == mask && (string?)value["Masked"] == "[PRIVATE]" &&
            value["Excluded"] == null && (string?)value["Text"] == "text", "Mask/exclusion/string precedence changed.");
    }
    private static void VerifyUnused<T>() => Check(ProbeConverter<T>.Converts == 0 &&
        ProbeConverter<T>.CanWrites == 0 && ProbeConverter<T>.Writes == 0, "Application converter serialization methods ran.");
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    #endregion
}

/// <summary>Methods return original inputs; no iterator instrumentation is involved.</summary>
[LogThis]
public sealed class DeferredSample
{
    #region Public Methods

    public object? Echo(object? value) => value;
    public IEnumerable EchoEnumerable(IEnumerable value) => value;
    [LogThis(Profile = "Deferred")] public object Profile(object value) => value;
    public void Fail(object value, [DontLogThis] Exception failure) => throw failure;

    #endregion
}

/// <summary>A consumable sequence remains untouched until application code enumerates it.</summary>
public sealed class DeferredSequence : IEnumerable
{
    #region Public Properties

    public int Enumerations { get; private set; }
    public int CountReads { get; private set; }
    public int StringReads { get; private set; }
    public int Count { get { CountReads++; throw new Exception("Count read."); } }

    #endregion

    #region Public Methods

    public IEnumerator GetEnumerator() { Enumerations++; return new[] { 1, 2 }.GetEnumerator(); }
    public override string ToString() { StringReads++; throw new Exception("ToString called."); }

    #endregion
}

/// <summary>A sequence fails immediately if capture requests an enumerator.</summary>
public sealed class ThrowingSequence : IEnumerable
{
    #region Public Properties

    public int Reads { get; private set; }

    #endregion

    #region Public Methods

    public IEnumerator GetEnumerator() { Reads++; throw new Exception("Enumerated."); }

    #endregion
}

/// <summary>Declared enumerable getters and conditional hooks must never run.</summary>
public sealed class DeferredProperties
{
    #region Public Properties

    [JsonIgnore] public int Reads { get; private set; }
    [JsonIgnore] public int Hooks { get; private set; }
    public int[] Array { get { Reads++; throw new Exception("Array getter."); } }
    public List<int> List { get { Reads++; throw new Exception("List getter."); } }
    public Dictionary<string, int> Map { get { Reads++; throw new Exception("Map getter."); } }
    public IEnumerable Sequence { get { Reads++; throw new Exception("Sequence getter."); } }
    public int[]? NullArray { get { Reads++; return null; } }
    public JObject Json { get { Reads++; throw new Exception("JSON getter."); } }
    public IEnumerable Password { get { Reads++; throw new Exception("Configured mask getter."); } }
    [MaskThisInLogs("[PRIVATE]")] public IEnumerable Masked { get { Reads++; throw new Exception("Masked getter."); } }
    [DontLogThis, MaskThisInLogs] public IEnumerable Excluded { get { Reads++; throw new Exception("Excluded getter."); } }
    public string Text => "text";
    [JsonIgnore] public bool ArraySpecified { get { Hooks++; throw new Exception("IsSpecified hook."); } }

    #endregion

    #region Public Methods

    public bool ShouldSerializeArray() { Hooks++; throw new Exception("ShouldSerialize hook."); }

    #endregion
}

/// <summary>Object declarations retain ordinary getter behavior.</summary>
public sealed class ObjectProperty(object value)
{
    #region Public Properties

    [JsonIgnore] public int Reads { get; private set; }
    public object Value { get { Reads++; return value; } }

    #endregion
}

/// <summary>A List subclass is outside the supported exact-type policy.</summary>
public sealed class CustomList : List<int>;
/// <summary>A Dictionary subclass is outside the supported exact-type policy.</summary>
public sealed class CustomDictionary : Dictionary<string, int>;

/// <summary>Independent counters prevent converter-cache interactions from hiding executions.</summary>
public sealed class ProbeConverter<T> : JsonConverter
{
    #region Public Fields

    public static int Constructors, Converts, CanWrites, Writes;

    #endregion

    #region Constructor

    public ProbeConverter() { Constructors++; }

    #endregion

    #region Public Members

    public override bool CanWrite { get { CanWrites++; return true; } }
    public override bool CanConvert(Type objectType) { Converts++; return true; }
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) { Writes++; writer.WriteValue("converted"); }
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) => throw new NotSupportedException();

    #endregion
}

/// <summary>Identifies the type-converter counter set.</summary>
public sealed class TypeProbe;
/// <summary>Identifies the property-converter counter set.</summary>
public sealed class PropertyProbe;
/// <summary>Identifies the property-item-converter counter set.</summary>
public sealed class ItemProbe;
/// <summary>Identifies the container-item-converter counter set.</summary>
public sealed class ContainerProbe;

/// <summary>A type-level converter is bypassed before construction.</summary>
[JsonConverter(typeof(ProbeConverter<TypeProbe>))]
public sealed class TypeConverted
{
    #region Public Properties

    public string Value => "ordinary";

    #endregion
}

/// <summary>Application property converters must not override internal masks or guards.</summary>
public sealed class ConverterProperties
{
    #region Public Properties

    [JsonConverter(typeof(ProbeConverter<PropertyProbe>))] public string Value => "ordinary";
    [JsonProperty(ItemConverterType = typeof(ProbeConverter<ItemProbe>))] public object Items => new List<int> { 1, 2 };
    [MaskThisInLogs("[PRIVATE]"), JsonConverter(typeof(ProbeConverter<PropertyProbe>))]
    public string Masked => throw new Exception("Masked getter.");
    [JsonConverter(typeof(ProbeConverter<PropertyProbe>))]
    public IEnumerable Sequence => throw new Exception("Sequence getter.");

    #endregion
}

/// <summary>Object container item converters would otherwise handle its property values.</summary>
[JsonObject(ItemConverterType = typeof(ProbeConverter<ContainerProbe>))]
public sealed class ContainerConverted
{
    #region Public Properties

    public string Value => "ordinary";

    #endregion
}

/// <summary>Metadata construction failures must be isolated without fallback serialization.</summary>
public sealed class ThrowingConstructor : JsonConverter
{
    #region Public Members

    public static int Calls;
    public ThrowingConstructor() { Calls++; throw new Exception("Converter constructor."); }
    public override bool CanConvert(Type objectType) => throw new Exception("CanConvert.");
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => throw new Exception("WriteJson.");
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) => throw new NotSupportedException();

    #endregion
}

/// <summary>A property converter constructor fails during metadata processing.</summary>
public sealed class ConstructorFailure
{
    #region Public Properties

    [JsonIgnore] public int Reads { get; private set; }
    [MaskThisInLogs, JsonConverter(typeof(ThrowingConstructor))]
    public string Secret { get { Reads++; throw new Exception("No fallback may read this getter."); } }

    #endregion
}

/// <summary>Extension-data dictionaries must not bypass the enumerable-property guard.</summary>
public sealed class DeferredExtensionData
{
    #region Public Properties

    [JsonIgnore] public int Reads { get; private set; }
    [JsonExtensionData] public IDictionary<string, JToken> Extra { get { Reads++; throw new Exception("Extension getter."); } }

    #endregion
}

/// <summary>Extension-data masking precedes enumerable markers.</summary>
public sealed class MaskedExtensionData
{
    #region Public Properties

    [JsonExtensionData, MaskThisInLogs("[EXTENSION]")]
    public IDictionary<string, JToken> Extra => throw new Exception("Masked extension getter.");

    #endregion
}

/// <summary>Excluded extension-data properties never contribute entries or a marker.</summary>
public sealed class ExcludedExtensionData
{
    #region Public Properties

    [JsonExtensionData, DontLogThis]
    public IDictionary<string, JToken> Extra => throw new Exception("Excluded extension getter.");

    #endregion
}
