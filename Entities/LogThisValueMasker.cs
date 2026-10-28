using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LogThis.Interfaces;

namespace LogThis.Entities;

/// <summary>Serializes logged values and masks configured property names throughout the JSON tree.</summary>
internal static class LogThisValueMasker
{
    #region Internal Methods

    /// <summary>Formats one completed, masked payload as unchanged text or a JSON preview wrapper.</summary>
    /// <param name="text">Fully serialized and masked capture, never an application value.</param>
    /// <param name="configuration">Selected profile's preview settings.</param>
    /// <returns>The original text or a valid JSON wrapper containing partial serialized text.</returns>
    internal static string FormatCapture(string text, ILogThisConfiguration configuration)
    {
        int length = configuration.MaxCapturedValueLength;
        if (!configuration.TruncateCapturedValues || text.Length <= length)
        {
            return text;
        }

        // Keep a UTF-16 surrogate pair together even when that leaves one unused preview unit.
        if (length > 0 && char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length]))
        {
            length--;
        }

        return new JObject { ["truncated"] = true, ["preview"] = text[..length] }.ToString(Formatting.None);
    }

    /// <summary>Serializes one value and masks matching properties at any depth.</summary>
    /// <param name="value">Value to serialize and mask.</param>
    /// <returns>Serialized JSON with matching property values replaced.</returns>
    internal static string MaskObject(object? value) => MaskJson(value).ToString(Formatting.None);

    /// <summary>Formats named arguments in declaration order, retaining positional output for legacy contexts.</summary>
    /// <param name="context">Method context whose arguments should be captured.</param>
    /// <returns>A JSON object of names and masked values, or a legacy positional list.</returns>
    internal static string MaskArguments(LogThisMethodContext context)
    {
        if (context.NamedArguments == null)
        {
            return MaskObjects(context.Arguments);
        }

        JObject arguments = [];

        foreach (KeyValuePair<string, object?> argument in context.NamedArguments)
        {
            // Mask each value before adding its parameter name, preserving property-based masking semantics.
            arguments.Add(argument.Key, argument.Value is LogThisMethodContext.MaskedArgument masked
                ? new JValue(masked.Replacement ?? LogThisRuntimeContext.Current!.Configuration.JsonMaskValue)
                : MaskJson(argument.Value));
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

        HashSet<string> names = new(runtime.Configuration.JsonFieldsToMask, StringComparer.OrdinalIgnoreCase);

        string mask = runtime.Configuration.JsonMaskValue;

        // Contracts contain capture-specific masks; never share them across scopes or configurations.
        LogThisContractResolver resolver = new(names, mask);

        JToken token = value == null
            ? JValue.CreateNull()
            : JToken.FromObject(value, JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = resolver
            }));

        MaskToken(token, names, mask, resolver);

        return token;
    }

    /// <summary>Replaces matching property values and descends through unmatched objects and arrays.</summary>
    /// <param name="token">Current JSON node to inspect.</param>
    /// <param name="names">Property names to mask, compared without case sensitivity.</param>
    /// <param name="mask">Replacement text for a matched value.</param>
    /// <param name="resolver">Identifies replacements already supplied by property contracts.</param>
    private static void MaskToken(JToken token, HashSet<string> names, string mask, LogThisContractResolver resolver)
    {
        if (token is JObject obj)
        {
            foreach (JProperty property in obj.Properties())
            {
                if (resolver.IsReplacement(property.Value))
                {
                    continue;
                }

                if (names.Contains(property.Name))
                {
                    property.Value = new JValue(mask);
                }
                else
                {
                    MaskToken(property.Value, names, mask, resolver);
                }
            }
        }
        else if (token is JArray array)
        {
            foreach (JToken item in array)
            {
                MaskToken(item, names, mask, resolver);
            }
        }
    }

    #endregion
}
