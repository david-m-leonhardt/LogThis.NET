namespace LogThis.Entities;

/// <summary>Captures the method metadata and argument values used by one LogThis invocation.</summary>
/// <remarks>
/// The aspect creates this runtime object by embedding method and class names from
/// <c>meta.Target</c> and capturing argument values when the method is invoked.
/// </remarks>
/// <param name="methodName">Name of the intercepted method.</param>
/// <param name="className">Name of its declaring class.</param>
/// <param name="arguments">Argument values supplied to this call.</param>
public sealed class LogThisMethodContext(string methodName, string className, object?[] arguments)
{
    #region Public Properties

    /// <summary>Gets the values of the method arguments for this call.</summary>
    /// <value>The array generated from <c>meta.Target.Parameters.ToValueArray()</c> for this invocation.</value>
    public object?[] Arguments { get; } = arguments;

    /// <summary>Gets the declaring class's name.</summary>
    /// <value>The name provided by <c>meta.Target.Method.DeclaringType.Name</c>.</value>
    public string ClassName { get; } = className;

    /// <summary>Gets the intercepted method's name.</summary>
    /// <value>The name provided by <c>meta.Target.Method.Name</c>.</value>
    public string MethodName { get; } = methodName;

    #endregion
}
