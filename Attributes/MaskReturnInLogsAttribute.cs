namespace LogThis.Attributes;

/// <summary>Replaces a method's entire successful logged result without inspecting the original value.</summary>
/// <remarks>
/// Requires LogThis instrumentation and enabled return-value capture. Uses the selected profile's
/// JsonMaskValue unless a literal replacement is supplied. Null results are masked identically.
/// Overrides require their own annotation. DontLogThis takes precedence. No-result methods produce
/// a weaving warning and retain normal logging. Explicit null replacements produce a weaving error.
/// Does not change application results or protect arguments, explicit logs, or exception text.
/// Async methods mask the normally awaited result; iterator execution behavior remains unchanged.
/// </remarks>
/// <example>
/// <code>
/// [LogThis, MaskReturnInLogs]
/// public string CreateToken() => "application token";
/// [LogThis, MaskReturnInLogs("[TOKEN]")]
/// public Task&lt;string&gt; CreateTokenAsync() => Task.FromResult("application token");
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MaskReturnInLogsAttribute : MaskInLogsAttribute
{
    #region Constructors

    /// <summary>Uses the selected profile's JsonMaskValue.</summary>
    public MaskReturnInLogsAttribute() { }

    /// <summary>Uses a fixed replacement, including an empty string.</summary>
    /// <param name="replacement">Replacement for the entire logged result.</param>
    /// <exception cref="ArgumentNullException">The explicit replacement is null.</exception>
    public MaskReturnInLogsAttribute(string replacement) : base(replacement) { }

    #endregion
}
