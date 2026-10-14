namespace LogThis.Entities;

/// <summary>Captures the method metadata and argument values used by one LogThis invocation.</summary>
/// <remarks>
/// The aspect creates this runtime object by embedding method and class names from
/// <c>meta.Target</c> and capturing argument values when the method is invoked.
/// </remarks>
/// <param name="methodName">Name of the intercepted method.</param>
/// <param name="className">Name of its declaring class.</param>
/// <param name="arguments">Argument values eligible for capture after parameter exclusions.</param>
public sealed class LogThisMethodContext(string methodName, string className, object?[] arguments)
{
    #region Public Properties

    /// <summary>Gets the included method argument values for this call.</summary>
    /// <remarks>Newly woven masked parameters contain replacement metadata instead of the original application value.</remarks>
    /// <value>The generated argument array, with excluded parameters omitted.</value>
    public object?[] Arguments { get; } = arguments;

    /// <summary>Gets included parameter names paired with values in declaration order.</summary>
    /// <remarks>Masked parameter entries retain their names and hold replacement metadata until argument formatting.</remarks>
    /// <value>Named arguments, or <see langword="null"/> for contexts created with the positional constructor.</value>
    public IReadOnlyList<KeyValuePair<string, object?>>? NamedArguments { get; private init; }

    /// <summary>Gets the declaring class's name.</summary>
    /// <value>The name provided by <c>meta.Target.Method.DeclaringType.Name</c>.</value>
    public string ClassName { get; } = className;

    /// <summary>Gets the intercepted method's name.</summary>
    /// <value>The name provided by <c>meta.Target.Method.Name</c>.</value>
    public string MethodName { get; } = methodName;

    #endregion

    #region Public Methods

    /// <summary>Creates argument-capture metadata using the selected configuration's JsonMaskValue.</summary>
    /// <returns>A marker that contains no application value and is resolved only when arguments are logged.</returns>
    public static object CreateMaskedArgument() => new MaskedArgument(null);

    /// <summary>Creates argument-capture metadata with a literal replacement, including an empty string.</summary>
    /// <param name="replacement">The complete replacement value.</param>
    /// <returns>A marker that contains no application value.</returns>
    /// <exception cref="ArgumentNullException">The replacement is null.</exception>
    public static object CreateMaskedArgument(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        return new MaskedArgument(replacement);
    }

    /// <summary>Creates a context from parameter names embedded at build time and their invocation values.</summary>
    /// <param name="methodName">Name of the intercepted method.</param>
    /// <param name="className">Name of its declaring class.</param>
    /// <param name="arguments">Included name/value pairs in declaration order.</param>
    /// <returns>A context with named arguments and a positional values view for compatibility.</returns>
    public static LogThisMethodContext FromNamedArguments(
        string methodName, string className, KeyValuePair<string, object?>[] arguments)
    {
        return new LogThisMethodContext(methodName, className, arguments.Select(argument => argument.Value).ToArray())
        {
            NamedArguments = Array.AsReadOnly((KeyValuePair<string, object?>[])arguments.Clone())
        };
    }

    #endregion

    #region Internal Type

    /// <summary>Carries replacement intent without retaining the original parameter.</summary>
    internal sealed record MaskedArgument(string? Replacement);

    #endregion
}
