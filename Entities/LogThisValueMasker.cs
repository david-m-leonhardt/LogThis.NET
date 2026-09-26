using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogThis.Entities;

/// <summary>Serializes logged values and masks configured property names throughout the JSON tree.</summary>
internal static class LogThisValueMasker
{
    #region Private Properties

    // Omit null members before masking to match the library's logged JSON representation.
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new LogThisContractResolver()
    };

    #endregion

    #region Internal Methods

    /// <summary>Serializes one value and masks matching properties at any depth.</summary>
    /// <param name="value">Value to serialize and mask.</param>
    /// <returns>Serialized JSON with matching property values replaced.</returns>
    internal static string MaskObject(object? value) => MaskJson(value).ToString(Formatting.None);

    /// <summary>Formats named arguments in declaration order, retaining positional output for legacy contexts.</summary>
    /// <param name="context">Method context whose arguments should be captured.</param>
    /// <returns>A JSON object of names and masked values, or a legacy positional list.</returns>
    internal static string MaskArguments(LogThisMethodContext context)
    {
        if (context.NamedArguments == null) return MaskObjects(context.Arguments);

        JObject arguments = new();
        foreach (KeyValuePair<string, object?> argument in context.NamedArguments)
        {
            // Mask each value before adding its parameter name, preserving property-based masking semantics.
            arguments.Add(argument.Key, MaskJson(argument.Value));
        }

        return arguments.ToString(Formatting.None);
    }

    /// <summary>Serializes and masks each argument, then combines them as a display list.</summary>
    /// <param name="values">Values to serialize and mask individually.</param>
    /// <returns>A bracketed list of masked serialized values.</returns>
    internal static string MaskObjects(object?[] values)
    {
        // Method arguments are individually serialized inside one display list.
        List<string> masked = [];
        foreach (object? value in values)
        {
            masked.Add(MaskObject(value));
        }

        return $"[{string.Join(", ", masked)}]";
    }

    #endregion

    #region Private Methods

    /// <summary>Serializes a value and masks matching properties in objects and arrays recursively.</summary>
    /// <param name="value">Value to serialize.</param>
    /// <returns>A JSON token with matched property values replaced.</returns>
    /// <exception cref="InvalidOperationException">A value is processed without an active LogThis scope.</exception>
    private static JToken MaskJson(object? value)
    {
        LogThisRuntime runtime = LogThisRuntimeContext.Current
            ?? throw new InvalidOperationException("No LogThis scope is active.");
        JToken token = value == null
            ? JValue.CreateNull()
            : JToken.FromObject(value, JsonSerializer.Create(SerializerSettings));
        HashSet<string> names = new(runtime.Configuration.JsonFieldsToMask, StringComparer.OrdinalIgnoreCase);
        MaskToken(token, names, runtime.Configuration.JsonMaskValue);
        return token;
    }

    /// <summary>Replaces matching property values and descends through unmatched objects and arrays.</summary>
    /// <param name="token">Current JSON node to inspect.</param>
    /// <param name="names">Property names to mask, compared without case sensitivity.</param>
    /// <param name="mask">Replacement text for a matched value.</param>
    private static void MaskToken(JToken token, HashSet<string> names, string mask)
    {
        if (token is JObject obj)
        {
            foreach (JProperty property in obj.Properties())
            {
                if (names.Contains(property.Name))
                {
                    property.Value = new JValue(mask);
                }
                else
                {
                    MaskToken(property.Value, names, mask);
                }
            }
        }
        else if (token is JArray array)
        {
            foreach (JToken item in array)
            {
                MaskToken(item, names, mask);
            }
        }
    }

    #endregion
}
