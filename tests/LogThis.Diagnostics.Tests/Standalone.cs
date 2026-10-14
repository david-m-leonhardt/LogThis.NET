using LogThis.Attributes;

/// <summary>Conflict diagnostics must run even when the compilation contains no LogThis usage.</summary>
public sealed class Standalone
{
    #region Constructors

    public Standalone([DontLogThis, MaskThisInLogs] string constructorValue) { } // LOGTHIS005

    #endregion

    #region Public Properties

    [DontLogThis, MaskThisInLogs] public string Secret => "secret"; // LOGTHIS005
    [MaskThisInLogs] public string MaskOnly => "secret";
    [DontLogThis] public string ExcludeOnly => "secret";

    #endregion

    #region Public Methods

    public void Accept([MaskThisInLogs, DontLogThis] string value) { } // LOGTHIS005
    public void Valid([MaskThisInLogs] string masked, [DontLogThis] string omitted) { }
    [DontLogThis, MaskReturnInLogs] public string ExcludedResult() => "secret"; // LOGTHIS004
    [MaskReturnInLogs] public string MaskOnlyResult() => "secret";
    [DontLogThis] public void ExcludedOnly() { }

    #endregion

    #region Nested Types

    /// <summary>Nested declarations participate in standalone checks.</summary>
    public sealed class Nested
    {
        #region Public Properties

        [MaskThisInLogs, DontLogThis] public string Secret => "secret"; // LOGTHIS005

        #endregion
    }

    #endregion
}
