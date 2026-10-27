using LogThis.Interfaces;

namespace LogThis.Entities
{
    /// <summary>Groups invocation data and selected runtime state for one generated event.</summary>
    internal class LogThisRuntimeValues
    {
        #region Internal Properties

        /// <summary>Event kind, message, enabled state, and severity for this event.</summary>
        internal required IAccessPointConfiguration AccessPoint { get; set; }

        /// <summary>Included argument payload for entry or exception events; null for exit events.</summary>
        internal object?[]? Arguments { get; set; }

        /// <summary>Method identity and named argument metadata used during capture.</summary>
        internal required LogThisMethodContext Context { get; set; }

        /// <summary>Previously measured elapsed milliseconds, or null when timing is unavailable or disabled.</summary>
        internal double? DurationMs { get; set; } = null;

        /// <summary>Original method failure for an exception event; null for entry or successful exit.</summary>
        internal Exception? Exception { get; set; }

        /// <summary>Whether enabled successful-result capture should use a replacement instead of the result.</summary>
        internal bool MaskReturn { get; set; } = false;

        /// <summary>Literal return mask, or null to use the selected configuration's JsonMaskValue.</summary>
        internal string? ReturnReplacement { get; set; } = null;

        /// <summary>Logger, selected configuration, custom components, and shared operation state.</summary>
        internal required LogThisRuntime Runtime { get; set; }

        /// <summary>Original successful result for ordinary capture; not populated for masked-result events.</summary>
        internal object? ReturnValue { get; set; }

        /// <summary>Checks whether event configuration or the logger's level filter suppresses this event.</summary>
        internal bool ShouldNotLog => !AccessPoint.LogAccessPoint || !Runtime.Logger.IsEnabled(AccessPoint.LogLevel);

        #endregion
    }
}
