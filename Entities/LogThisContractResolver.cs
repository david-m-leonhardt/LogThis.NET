using System.Reflection;
using LogThis.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LogThis.Entities;

/// <summary>Omits excluded properties before the serializer can read their values.</summary>
internal sealed class LogThisContractResolver : DefaultContractResolver
{
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

        return property;
    }
}
