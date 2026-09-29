using System.Reflection;
using LogThis.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace LogThis.Entities;

/// <summary>Excludes or replaces ordinary property values before the serializer can read them.</summary>
/// <param name="names">Configured JSON property names to mask for this capture.</param>
/// <param name="mask">Configured replacement for this capture.</param>
internal sealed class LogThisContractResolver(HashSet<string> names, string mask) : DefaultContractResolver
{
    #region Private Fields

    // Token identity preserves explicit replacements during the dictionary/JSON fallback pass.
    private readonly HashSet<JToken> replacements = new(ReferenceEqualityComparer.Instance);

    #endregion

    #region Internal Methods

    /// <summary>Identifies values already replaced through ordinary property contracts.</summary>
    internal bool IsReplacement(JToken token) => replacements.Contains(token);

    #endregion

    #region Protected Methods

    /// <summary>Builds a property contract with LogThis exclusions taking precedence over JSON inclusion.</summary>
    /// <param name="member">Member being considered for serialization.</param>
    /// <param name="memberSerialization">Serialization mode of the containing object.</param>
    /// <returns>The property contract used by LogThis serialization.</returns>
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        if (member is PropertyInfo && member.IsDefined(typeof(DontLogThisAttribute), inherit: false))
        {
            property.Ignored = true;
        }
        else if (member is PropertyInfo && !property.Ignored)
        {
            MaskThisInLogsAttribute? attribute = member.GetCustomAttribute<MaskThisInLogsAttribute>(inherit: false);
            if (attribute != null || names.Contains(property.PropertyName!))
            {
                MaskedPropertyValue replacement = new(attribute?.Replacement ?? mask, replacements);
                property.ValueProvider = replacement;
                property.Converter = replacement;
                property.PropertyType = typeof(string);
                property.Readable = true;
                property.NullValueHandling = NullValueHandling.Include;
                property.DefaultValueHandling = DefaultValueHandling.Include;
                // Conditional serialization hooks can read the original property too.
                property.ShouldSerialize = null;
                property.GetIsSpecified = null;
            }
        }

        return property;
    }

    #endregion

    #region Private Class

    /// <summary>Supplies and writes a replacement without accessing the application object.</summary>
    private sealed class MaskedPropertyValue(string replacement, HashSet<JToken> replacements) : JsonConverter, IValueProvider
    {
        #region Public Methods

        /// <inheritdoc />
        public object GetValue(object target) => replacement;

        /// <inheritdoc />
        public void SetValue(object target, object? value) => throw new NotSupportedException("LogThis only serializes captures.");

        /// <inheritdoc />
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        /// <inheritdoc />
        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            => throw new NotSupportedException("LogThis only serializes captures.");

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteValue(replacement);
            if (writer is JTokenWriter tokenWriter && tokenWriter.CurrentToken != null)
                replacements.Add(tokenWriter.CurrentToken);
        }

        #endregion
    }

    #endregion
}
