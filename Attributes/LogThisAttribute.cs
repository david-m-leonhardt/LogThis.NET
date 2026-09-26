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

    /// <summary>Instruments eligible public methods of an annotated class, respecting method exclusions.</summary>
    /// <param name="builder">Builder for the annotated class.</param>
    public void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        // Class-level use advises methods; method-level use is handled by OverrideMethodAspect.
        MethodTemplateSelector templates = new(
            nameof(OverrideMethod),
            nameof(OverrideAsyncMethod),
            null, null, null, null,
            true, false);

        foreach (IMethod method in builder.Target.Methods)
        {
            if (method.Accessibility == Accessibility.Public && !method.IsAbstract &&
                !method.Attributes.OfAttributeType(typeof(DontLogThisAttribute)).Any())
            {
                builder.With(method).Override(templates);
            }
        }
    }

    /// <summary>Instruments a method unless it explicitly opts out of LogThis.</summary>
    /// <param name="builder">Builder for the annotated method.</param>
    public override void BuildAspect(IAspectBuilder<IMethod> builder)
    {
        if (builder.Target.Attributes.OfAttributeType(typeof(DontLogThisAttribute)).Any())
        {
            builder.SkipAspect();
            return;
        }

        base.BuildAspect(builder);
    }

    /// <summary>Wraps an awaitable method and logs its completed result or failure.</summary>
    /// <returns>The original method's result after it completes, if any.</returns>
    public override async Task<dynamic?> OverrideAsyncMethod()
    {
        List<KeyValuePair<string, object?>> arguments = new();
        CaptureArguments(arguments);
        LogThisMethodContext context = LogThisMethodContext.FromNamedArguments(
            meta.Target.Method.Name,
            meta.Target.Method.DeclaringType.Name,
            arguments.ToArray());

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
        List<KeyValuePair<string, object?>> arguments = new();
        CaptureArguments(arguments);
        LogThisMethodContext context = LogThisMethodContext.FromNamedArguments(
            meta.Target.Method.Name,
            meta.Target.Method.DeclaringType.Name,
            arguments.ToArray());

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

    #region Private Methods

    // The compile-time loop emits reads only for included parameters.
    [Template]
    private void CaptureArguments(List<KeyValuePair<string, object?>> arguments)
    {
        foreach (IParameter parameter in meta.Target.Parameters)
        {
            if (!parameter.Attributes.OfAttributeType(typeof(DontLogThisAttribute)).Any())
            {
                arguments.Add(new KeyValuePair<string, object?>(
                    parameter.Name, parameter.RefKind == RefKind.Out ? null : parameter.Value));
            }
        }
    }

    #endregion
}
