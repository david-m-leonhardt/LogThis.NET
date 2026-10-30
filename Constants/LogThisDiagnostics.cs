using Metalama.Framework.Aspects;
using Metalama.Framework.Diagnostics;

namespace LogThis.Constants
{
    /// <summary>Stable build-time diagnostics for invalid or ineffective LogThis annotations.</summary>
    [CompileTime]
    internal class LogThisDiagnostics
    {
        #region Public Properties

        /// <summary>Rejects an explicit null return replacement during weaving.</summary>
        [CompileTime]
        internal static readonly DiagnosticDefinition NullReturnMask = new("LOGTHIS001", Severity.Error,
            "MaskReturnInLogs replacement cannot be null. Omit the argument to use JsonMaskValue.");

        /// <summary>Warns when an instrumented method has no usable result to mask.</summary>
        [CompileTime]
        internal static readonly DiagnosticDefinition NoReturnToMask = new("LOGTHIS002", Severity.Warning,
            "MaskReturnInLogs has no usable result to mask on this method. Logging remains unchanged.");

        /// <summary>Warns when a method exclusion makes its LogThis annotation ineffective.</summary>
        internal static readonly DiagnosticDefinition ExcludedLogging = new("LOGTHIS003", Severity.Warning,
           "DontLogThis excludes this method, so LogThis has no effect. Remove LogThis or remove the exclusion.");

        /// <summary>Warns when a method exclusion makes return masking ineffective.</summary>
        internal static readonly DiagnosticDefinition ExcludedReturnMask = new("LOGTHIS004", Severity.Warning,
            "DontLogThis excludes this method, so MaskReturnInLogs has no effect. Remove the mask or remove the exclusion.");

        /// <summary>Warns when data exclusion takes precedence over masking on the same declaration.</summary>
        internal static readonly DiagnosticDefinition ExcludedDataMask = new("LOGTHIS005", Severity.Warning,
            "DontLogThis omits this declaration, so MaskThisInLogs has no effect. Remove the mask or remove the exclusion.");

        /// <summary>Warns when a covered method repeats its containing class profile selection.</summary>
        internal static readonly DiagnosticDefinition RedundantProfile = new("LOGTHIS006", Severity.Warning,
            "This method selects the same LogThis profile as its containing class, which already covers it. Remove the method's LogThis attribute or select a different profile.");

        #endregion
    }
}
