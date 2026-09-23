using System.Collections;
using JsonMasking;
using LogThis.Entities;
using LogThis.Extensions;
using Newtonsoft.Json;

namespace LogThis.Attributes;

/// <summary>Serializes logged values and masks configured JSON object fields.</summary>
internal static class LogThisValueMasker
{
    #region Private Properties

    // Omit null members before masking to match the library's logged JSON representation.
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    #endregion

    #region Internal Methods

    /// <summary>Masks one value, splitting a top-level list into separately masked items.</summary>
    /// <remarks>Nested lists remain inside their containing object's JSON for the masking library to process.</remarks>
    /// <param name="value">Value to serialize and mask.</param>
    /// <returns>Serialized text with configured object fields replaced.</returns>
    internal static string MaskObject(object? value)
    {
        // The masking library operates on object roots, so top-level list items are handled separately.
        if (value is IList list)
        {
            object?[] values = new object?[list.Count];
            list.CopyTo(values, 0);
            return MaskObjects(values);
        }

        return MaskJson(value);
    }

    /// <summary>Serializes each argument, masks JSON object roots, and combines them as a display list.</summary>
    /// <param name="values">Values to serialize and mask individually.</param>
    /// <returns>A bracketed list of masked serialized values.</returns>
    internal static string MaskObjects(object?[] values)
    {
        // Method arguments are individually serialized inside one display list.
        List<string> masked = [];
        foreach (object? value in values)
        {
            masked.Add(MaskJson(value));
        }

        return $"[{string.Join(", ", masked)}]";
    }

    #endregion

    #region Private Methods

    /// <summary>Serializes an object and masks configured fields when its root is a JSON object.</summary>
    /// <remarks>Non-object JSON roots are returned without field masking.</remarks>
    /// <param name="value">Value to serialize.</param>
    /// <returns>Masked JSON object text, or the serialized value for other roots.</returns>
    /// <exception cref="InvalidOperationException">A maskable object is processed without an active LogThis scope.</exception>
    private static string MaskJson(object? value)
    {
        string json = value != null ? JsonConvert.SerializeObject(value, SerializerSettings) : string.Empty;
        if (!json.IsValidJson()) return json;

        LogThisRuntime runtime = LogThisRuntimeContext.Current
            ?? throw new InvalidOperationException("No LogThis scope is active.");
        string masked = json.MaskFields([.. runtime.Configuration.JsonFieldsToMask], runtime.Configuration.JsonMaskValue)
            .Replace("\r\n", "");
        while (masked.Contains("  "))
        {
            masked = masked.Replace("  ", " ");
        }

        return masked;
    }

    #endregion
}
