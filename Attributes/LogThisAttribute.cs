using LogThis.Entities;
using LogThis.Constants;
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

    #region Public Properties

    /// <summary>Selects a complete named configuration; null selects the default configuration.</summary>
    /// <remarks>Names are trimmed and matched case-insensitively. Method attributes override class selection, including explicit default selection. Unknown names fail when invoked in an active scope.</remarks>
    public string? Profile { get; set; }

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
                !method.Attributes.OfAttributeType(typeof(DontLogThisAttribute)).Any() &&
                !method.Attributes.OfAttributeType(typeof(LogThisAttribute)).Any())
            {
                ValidateReturnMask(builder.With(method));
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

        ValidateReturnMask(builder);
        base.BuildAspect(builder);
    }

    /// <summary>Wraps an awaitable method and logs its completed result or failure.</summary>
    /// <returns>The original method's result after it completes, if any.</returns>
    public override async Task<dynamic?> OverrideAsyncMethod()
    {
        using IDisposable? profileScope = LogThisRuntimeLogger.SelectProfile(meta.CompileTime(Profile));
        List<KeyValuePair<string, object?>> arguments = [];
        CaptureArguments(arguments);
        LogThisMethodContext context = LogThisMethodContext.FromNamedArguments(
            meta.Target.Method.Name,
            meta.Target.Method.DeclaringType.Name,
            [.. arguments]);

        LogThisRuntimeLogger.Entry(context);

        long? durationStart = LogThisRuntimeLogger.StartDuration();

        try
        {
            // Await so exit and exception events reflect the eventual outcome.
            dynamic? result = await meta.ProceedAsync();
            LogExit(context, result, LogThisRuntimeLogger.FinishDuration(durationStart));
            return result;
        }
        catch (Exception exception)
        {
            LogThisRuntimeLogger.Exception(context, exception, LogThisRuntimeLogger.FinishDuration(durationStart));
            throw;
        }
    }

    /// <summary>Wraps a synchronous method with entry, exit, and exception events.</summary>
    /// <returns>The original method's result, if any.</returns>
    public override dynamic? OverrideMethod()
    {
        using IDisposable? profileScope = LogThisRuntimeLogger.SelectProfile(meta.CompileTime(Profile));
        List<KeyValuePair<string, object?>> arguments = [];
        CaptureArguments(arguments);
        LogThisMethodContext context = LogThisMethodContext.FromNamedArguments(
            meta.Target.Method.Name,
            meta.Target.Method.DeclaringType.Name,
            [.. arguments]);

        LogThisRuntimeLogger.Entry(context);

        long? durationStart = LogThisRuntimeLogger.StartDuration();

        try
        {
            dynamic? result = meta.Proceed();
            LogExit(context, result, LogThisRuntimeLogger.FinishDuration(durationStart));
            return result;
        }
        catch (Exception exception)
        {
            LogThisRuntimeLogger.Exception(context, exception, LogThisRuntimeLogger.FinishDuration(durationStart));
            throw;
        }
    }

    #endregion

    #region Private Methods

    // The compile-time loop emits value reads only for included, unmasked parameters.
    [Template]
    private void CaptureArguments(List<KeyValuePair<string, object?>> arguments)
    {
        foreach (IParameter parameter in meta.Target.Parameters)
        {
            if (!parameter.Attributes.OfAttributeType(typeof(DontLogThisAttribute)).Any())
            {
                IAttribute? mask = parameter.Attributes.OfAttributeType(typeof(MaskThisInLogsAttribute)).SingleOrDefault();
                if (mask != null)
                {
                    // Only metadata is captured: the original parameter is never read for logging.
                    if (mask.ConstructorArguments.Length == 0)
                        arguments.Add(new KeyValuePair<string, object?>(parameter.Name, LogThisMethodContext.CreateMaskedArgument()));
                    else
                    {
                        string replacement = meta.CompileTime(GetMaskReplacement(mask));
                        arguments.Add(new KeyValuePair<string, object?>(parameter.Name, LogThisMethodContext.CreateMaskedArgument(replacement)));
                    }
                }
                else
                {
                    arguments.Add(new KeyValuePair<string, object?>(
                        parameter.Name, parameter.RefKind == RefKind.Out ? null : parameter.Value));
                }
            }
        }
    }

    /// <summary>Validates literal parameter replacements while weaving, not while application code runs.</summary>
    [CompileTime]
    private static string GetMaskReplacement(IAttribute mask)
        => (string?)mask.ConstructorArguments[0].Value
            ?? throw new ArgumentException("MaskThisInLogs replacement cannot be null. Omit the argument to use JsonMaskValue.");

    /// <summary>Passes only replacement metadata to the logger when the result is masked.</summary>
    [Template]
    private void LogExit(LogThisMethodContext context, dynamic? result, double? durationMs)
    {
        IAttribute? mask = meta.Target.Method.Attributes.OfAttributeType(typeof(MaskReturnInLogsAttribute)).SingleOrDefault();
        if (mask != null && meta.Target.Method.GetAsyncInfo().ResultType.SpecialType != SpecialType.Void)
        {
            var replacement = meta.CompileTime(mask.ConstructorArguments.Length == 0 ? null : (string?)mask.ConstructorArguments[0].Value);
            LogThisRuntimeLogger.ExitMasked(context, replacement, durationMs);
        }
        else
        {
            LogThisRuntimeLogger.Exit(context, result, durationMs);
        }
    }

    /// <summary>Reports declaration-local return-mask diagnostics for instrumented methods.</summary>
    [CompileTime]
    private static void ValidateReturnMask(IAspectBuilder<IMethod> builder)
    {
        IAttribute? mask = builder.Target.Attributes.OfAttributeType(typeof(MaskReturnInLogsAttribute)).SingleOrDefault();

        if (mask == null)
        {
            return;
        }

        if (mask.ConstructorArguments.Length != 0 && mask.ConstructorArguments[0].Value == null)
        {
            builder.Diagnostics.Report(LogThisDiagnostics.NullReturnMask);
        }

        if (builder.Target.GetAsyncInfo().ResultType.SpecialType == SpecialType.Void)
        {
            builder.Diagnostics.Report(LogThisDiagnostics.NoReturnToMask);
        }
    }

    #endregion
}
