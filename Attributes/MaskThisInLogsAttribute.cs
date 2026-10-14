namespace LogThis.Attributes;

/// <summary>Replaces an entire parameter or property value in LogThis captures without inspecting its contents.</summary>
/// <remarks>
/// Applies to ordinary properties serialized by LogThis, including nested objects and collection items.
/// DontLogThis takes precedence. An explicit replacement overrides configured name masking; otherwise
/// JsonMaskValue is used. Null-valued properties also receive the replacement without being read.
/// Does not modify application objects, explicit application logs, or exception-message text.
/// Parameter masks retain the parameter name on entry and exception events. They do not follow
/// values into return capture or other calls. Property masks skip getters.
/// An unchanged inherited property retains its annotation. Overrides (including implementations
/// of abstract properties) and new hiding properties require their own annotation. Unannotated
/// overrides remain eligible for capture unless excluded or matched by configured name masking.
/// Annotated declarations use their explicit replacement or the selected profile's JsonMaskValue.
/// </remarks>
/// <example>
/// <code>
/// public class BaseModel
/// {
///     [MaskThisInLogs] public virtual string Secret => "base";
/// }
/// public class DerivedModel : BaseModel
/// {
///     [MaskThisInLogs("[PRIVATE]")] public override string Secret => "derived";
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class MaskThisInLogsAttribute : Attribute
{
    #region Constructors

    /// <summary>Uses the active capture's configured JsonMaskValue.</summary>
    public MaskThisInLogsAttribute() { }

    /// <summary>Uses a fixed replacement, including an empty string if supplied.</summary>
    /// <param name="replacement">Replacement for the entire parameter or property value.</param>
    /// <exception cref="ArgumentNullException">The explicit replacement is null.</exception>
    public MaskThisInLogsAttribute(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Replacement = replacement;
    }

    #endregion

    #region Public Properties

    /// <summary>Gets the explicit replacement, or null to use configured JsonMaskValue.</summary>
    public string? Replacement { get; }

    #endregion
}
