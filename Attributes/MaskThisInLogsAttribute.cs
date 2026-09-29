namespace LogThis.Attributes;

/// <summary>Replaces an entire property value in LogThis captures without invoking its getter.</summary>
/// <remarks>
/// Applies to ordinary properties serialized by LogThis, including nested objects and collection items.
/// DontLogThis takes precedence. An explicit replacement overrides configured name masking; otherwise
/// JsonMaskValue is used. Null-valued properties also receive the replacement without being read.
/// Does not modify application objects, explicit application logs, or exception-message text.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MaskThisInLogsAttribute : Attribute
{
    #region Constructors

    /// <summary>Uses the active capture's configured JsonMaskValue.</summary>
    public MaskThisInLogsAttribute() { }

    /// <summary>Uses a fixed replacement, including an empty string if supplied.</summary>
    /// <param name="replacement">Replacement for the entire property value.</param>
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
