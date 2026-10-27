using System.Collections;
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
    #region Private Properties

    // Token identity preserves explicit replacements during the dictionary/JSON fallback pass.
    private readonly HashSet<JToken> replacements = new(ReferenceEqualityComparer.Instance);

    #endregion

    #region Internal Methods

    /// <summary>Identifies values already replaced through ordinary property contracts.</summary>
    internal bool IsReplacement(JToken token) => replacements.Contains(token);

    #endregion

    #region Protected Methods

    /// <summary>Restricts already available sequences while preserving Newtonsoft's prebuilt JSON handling.</summary>
    protected override JsonContract CreateContract(Type objectType)
    {
        if (IsEnumerable(objectType) && !typeof(JToken).IsAssignableFrom(objectType) &&
            !objectType.IsArray && !IsOrdinaryCollection(objectType))
        {
            // Avoid array-contract setup and callbacks for an unsupported sequence.
            return new JsonPrimitiveContract(objectType)
            {
                Converter = new ReplacementValue("[Not enumerated]", replacements, false)
            };
        }

        JsonContract contract = base.CreateContract(objectType);
        if (contract is JsonContainerContract container)
        {
            // Newtonsoft may have constructed this converter; it must never serialize a capture.
            container.ItemConverter = null;
        }
        return contract;
    }

    /// <summary>Stops enumerable extension-data properties from bypassing the normal property contracts.</summary>
    protected override JsonObjectContract CreateObjectContract(Type objectType)
    {
        JsonObjectContract contract = base.CreateObjectContract(objectType);
        if (contract.ExtensionDataGetter == null) return contract;

        for (Type? type = objectType; type != null; type = type.BaseType)
        {
            foreach (PropertyInfo member in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!IsEnumerable(member.PropertyType) ||
                    member.GetCustomAttribute<JsonExtensionDataAttribute>()?.WriteData != true) continue;

                // Extension data normally bypasses ValueProvider; emit the property's guarded value instead.
                contract.ExtensionDataGetter = null;
                if (!contract.Properties.Any(p => p.UnderlyingName == member.Name && p.DeclaringType == member.DeclaringType))
                    contract.Properties.Add(CreateProperty(member, contract.MemberSerialization));
            }
        }
        return contract;
    }

    /// <summary>Builds a property contract with LogThis exclusions taking precedence over JSON inclusion.</summary>
    /// <param name="member">Member being considered for serialization.</param>
    /// <param name="memberSerialization">Serialization mode of the containing object.</param>
    /// <returns>The property contract used by LogThis serialization.</returns>
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        // Clear application converters before installing LogThis's own replacement converter.
        property.Converter = null;
        property.ItemConverter = null;
        if (member is PropertyInfo extensionProperty && IsEnumerable(extensionProperty.PropertyType) &&
            member.GetCustomAttribute<JsonExtensionDataAttribute>()?.WriteData == true &&
            !member.IsDefined(typeof(JsonIgnoreAttribute), inherit: true))
        {
            property.Ignored = false;
        }
        if (member is PropertyInfo && member.IsDefined(typeof(DontLogThisAttribute), inherit: false))
        {
            property.Ignored = true;
        }
        else if (member is PropertyInfo && !property.Ignored)
        {
            MaskThisInLogsAttribute? attribute = member.GetCustomAttribute<MaskThisInLogsAttribute>(inherit: false);
            if (attribute != null || names.Contains(property.PropertyName!))
            {
                ReplaceProperty(property, attribute?.Replacement ?? mask);
            }
            else if (IsEnumerable(((PropertyInfo)member).PropertyType))
            {
                // Declared types are sufficient: do not read even null or materialized properties.
                ReplaceProperty(property, "[Not read]");
            }
        }

        return property;
    }

    /// <summary>Suppresses application type converters without constructing them.</summary>
    protected override JsonConverter? ResolveContractConverter(Type objectType) => null;

    #endregion

    #region Private Methods

    /// <summary>Identifies enumerable declarations without reading values; strings retain scalar handling.</summary>
    private static bool IsEnumerable(Type type) => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>Allows exact ordinary collections, not custom subclasses or interface implementations.</summary>
    private static bool IsOrdinaryCollection(Type type) => type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(List<>) || type.GetGenericTypeDefinition() == typeof(Dictionary<,>));

    /// <summary>Supplies a fixed value and disables conditional hooks that could access the skipped getter.</summary>
    private void ReplaceProperty(JsonProperty property, string text)
    {
        ReplacementValue replacement = new(text, replacements);
        property.ValueProvider = replacement;
        property.Converter = replacement;
        property.PropertyType = typeof(string);
        property.Readable = true;
        property.NullValueHandling = NullValueHandling.Include;
        property.DefaultValueHandling = DefaultValueHandling.Include;
        property.ShouldSerialize = null;
        property.GetIsSpecified = null;
    }

    #endregion

    #region Private Class

    /// <summary>Supplies and writes a replacement without accessing the application object.</summary>
    private sealed class ReplacementValue(string replacement, HashSet<JToken> replacements, bool preserveReplacement = true) : JsonConverter, IValueProvider
    {
        #region Public Methods

        /// <inheritdoc />
        public object GetValue(object target) => replacement;

        /// <inheritdoc />
        public void SetValue(object target, object? value) => throw new NotSupportedException("LogThis only serializes captures.");

        /// <inheritdoc />
        public override bool CanConvert(Type objectType) => true;

        /// <inheritdoc />
        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            => throw new NotSupportedException("LogThis only serializes captures.");

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteValue(replacement);
            if (preserveReplacement && writer is JTokenWriter tokenWriter && tokenWriter.CurrentToken != null)
                replacements.Add(tokenWriter.CurrentToken);
        }

        #endregion
    }

    #endregion
}
