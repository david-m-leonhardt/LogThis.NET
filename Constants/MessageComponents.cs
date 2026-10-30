namespace LogThis.Constants
{
    /// <summary>Internal placeholders and defaults used to construct LogThis log events.</summary>
    internal static class MessageComponents
    {
        /// <summary>Default logging category for manually created scopes.</summary>
        internal const string DefaultCategoryName = "LogThis";
        /// <summary>Default separator between message-template components.</summary>
        internal const string DefaultDelimeter = " | ";
        /// <summary>Default replacement text for masked JSON fields.</summary>
        internal const string DefaultJsonMaskValue = "*****";
        /// <summary>Placeholder for serialized method arguments.</summary>
        internal const string LogThisArguments = "{LogThisArguments}";
        /// <summary>Placeholder for the declaring class name.</summary>
        internal const string LogThisClass = "{LogThisClass}";
        /// <summary>Placeholder for the correlation id.</summary>
        internal const string LogThisCorrelationId = "{LogThisCorrelationId}";
        /// <summary>Placeholder for the duration text.</summary>
        internal const string LogThisDurationMs = "{LogThisDurationMs}";
        /// <summary>Placeholder for the method name.</summary>
        internal const string LogThisMethod = "{LogThisMethod}";
        /// <summary>Placeholder for the serialized return value.</summary>
        internal const string LogThisReturnValue = "{LogThisReturnValue}";
        /// <summary>Placeholder for the existing activity span ID.</summary>
        internal const string LogThisSpanId = "{LogThisSpanId}";
        /// <summary>Placeholder for the existing activity trace ID.</summary>
        internal const string LogThisTraceId = "{LogThisTraceId}";
        /// <summary>Placeholder for the entry-event text.</summary>
        internal const string OnEntryLabel = "{LogThisOnEntry}";
        /// <summary>Placeholder for the exception-event text.</summary>
        internal const string OnExceptionLabel = "{LogThisOnException}";
        /// <summary>Placeholder for the exit-event text.</summary>
        internal const string OnExitLabel = "{LogThisOnExit}";

        /// <summary>Gets a fresh mapping of library placeholders to optional human-readable labels.</summary>
        internal static Dictionary<string, string> LabelMap => new()
        {
            { LogThisArguments, "Arguments: " },
            { LogThisClass, "Class: " },
            { LogThisCorrelationId, "Correlation ID: " },
            { LogThisDurationMs, "Duration ms: " },
            { LogThisMethod, "Method: " },
            { LogThisReturnValue, "Return value: " },
            { LogThisSpanId, "Span ID: " },
            { LogThisTraceId, "Trace ID: " }
        };
    }
}
