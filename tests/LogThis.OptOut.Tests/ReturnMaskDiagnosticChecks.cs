using LogThis.Attributes;

#if RETURN_MASK_DIAGNOSTICS
/// <summary>Build-only fixtures exercise no-result warnings on both instrumentation paths.</summary>
[LogThis]
public sealed class ReturnMaskWarningProbe
{
    #region Public Methods

    [MaskReturnInLogs] public void VoidResult() { }
    [LogThis, MaskReturnInLogs] public Task TaskResult() => Task.CompletedTask;
    [MaskReturnInLogs] public ValueTask ValueTaskResult() => ValueTask.CompletedTask;

    #endregion
}
#endif

#if RETURN_MASK_NULL
/// <summary>Build-only fixtures must reject explicit null replacements during weaving.</summary>
[LogThis]
public sealed class ReturnMaskErrorProbe
{
    #region Public Methods

    [MaskReturnInLogs(null!)] public string ClassResult() => "original";
    [LogThis, MaskReturnInLogs(null!)] public Task<string> MethodResult() => Task.FromResult("original");

    #endregion
}
#endif
