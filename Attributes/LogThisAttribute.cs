using LogThis.Entities;
using Metalama.Framework.Advising;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace LogThis.Attributes;

/// <summary>
/// Weaves entry, exit, and exception logging into a method or every public concrete method of a class.
/// Generated calls delegate runtime work to <see cref="LogThisRuntimeLogger"/>.
/// </summary>
[AttributeUsage(validOn: AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class LogThisAttribute : OverrideMethodAspect, IAspect<INamedType>
{
    #region Constructor

    /// <summary>Creates an aspect that uses async templates for awaitable methods.</summary>
    public LogThisAttribute()
    {
        UseAsyncTemplateForAnyAwaitable = true;
    }

    #endregion

    #region Public Methods

    // Applies the method templates to eligible members of an annotated class.
    // The explicit Metalama interface member is not included in consumer-facing XML output.
    void IAspect<INamedType>.BuildAspect(IAspectBuilder<INamedType> builder)
    {
        // Class-level use advises methods; method-level use is handled by OverrideMethodAspect.
        MethodTemplateSelector templates = new(
            nameof(OverrideMethod),
            nameof(OverrideAsyncMethod),
            null, null, null, null,
            true, false);

        foreach (IMethod method in builder.Target.Methods)
        {
            if (method.Accessibility == Accessibility.Public && !method.IsAbstract)
            {
                builder.With(method).Override(templates);
            }
        }
    }

    /// <summary>Wraps an awaitable method and logs its completed result or failure.</summary>
    /// <returns>The original method's result after it completes, if any.</returns>
    public override async Task<dynamic?> OverrideAsyncMethod()
    {
        LogThisMethodContext context = new(
            meta.Target.Method.Name,
            meta.Target.Method.DeclaringType.Name,
            meta.Target.Parameters.ToValueArray());

        global::LogThis.Entities.LogThisRuntimeLogger.Entry(context);

        try
        {
            // Await so exit and exception events reflect the eventual outcome.
            dynamic? result = await meta.ProceedAsync();
            global::LogThis.Entities.LogThisRuntimeLogger.Exit(context, result);
            return result;
        }
        catch (Exception exception)
        {
            global::LogThis.Entities.LogThisRuntimeLogger.Exception(context, exception);
            throw;
        }
    }

    /// <summary>Wraps a synchronous method with entry, exit, and exception events.</summary>
    /// <returns>The original method's result, if any.</returns>
    public override dynamic? OverrideMethod()
    {
        LogThisMethodContext context = new(
            meta.Target.Method.Name,
            meta.Target.Method.DeclaringType.Name,
            meta.Target.Parameters.ToValueArray());

        global::LogThis.Entities.LogThisRuntimeLogger.Entry(context);

        try
        {
            dynamic? result = meta.Proceed();
            global::LogThis.Entities.LogThisRuntimeLogger.Exit(context, result);
            return result;
        }
        catch (Exception exception)
        {
            global::LogThis.Entities.LogThisRuntimeLogger.Exception(context, exception);
            throw;
        }
    }

    #endregion
}
