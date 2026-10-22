using LogThis.Extensions;
using LogThis.Interfaces;

namespace LogThis.Entities
{
    /// <summary>Mutable options that control method-boundary events and sensitive-field masking.</summary>
    public sealed class LogThisConfiguration() : ILogThisConfiguration
    {
        #region Public Properties

        /// <summary>Writes internal logging failures to the console at Debug level, independent of configured providers.</summary>
        /// <value><see langword="false"/> by default.</value>
        public bool DebugLogThis { get; set; } = false;

        /// <summary>Case-insensitive JSON property names to redact at any depth in logged values.</summary>
        /// <remarks>Ordinary property matches skip getters and emit a whole-value replacement even for nulls. Explicit MaskThisInLogs replacements take precedence; DontLogThis omits properties. Dictionary/JSON masking retains a post-serialization fallback.</remarks>
        /// <value>An initially empty, mutable list.</value>
        public List<string> JsonFieldsToMask { get; set; } = [];

        /// <summary>Replacement text for each masked JSON field.</summary>
        /// <remarks>Also used by MaskThisInLogs without an explicit replacement. Applies per capture without mutating application values.</remarks>
        /// <value><c>*****</c> by default.</value>
        public string JsonMaskValue { get; set; } = Constants.MessageComponents.DefaultJsonMaskValue;

        /// <summary>Includes the declaring class name in each event.</summary>
        /// <value><see langword="false"/> by default.</value>
        public bool LogClassName { get; set; } = false;

        /// <summary>Includes friendly component labels in message templates without changing structured values.</summary>
        /// <value><see langword="true"/> by default.</value>
        public bool LogComponentLabels { get; set; } = true;

        /// <summary>Includes the chain's correlation ID, generating it lazily for the first eligible event that needs it.</summary>
        /// <remarks>The 16-character cryptographic alphanumeric ID is case-sensitive and shared across nested scopes, profiles, and awaits. Disabled or filtered events do not request an ID. One generation attempt is made per chain; failure permits logging without an ID, with best-effort debug diagnostics. Only LogThis events are enriched.</remarks>
        /// <value><see langword="true"/> by default.</value>
        public bool LogCorrelationId { get; set; } = true;

        /// <summary>Includes serialized arguments on entry and exception events.</summary>
        /// <value><see langword="false"/> by default.</value>
        public bool LogMethodArguments { get; set; } = false;

        /// <summary>Includes numeric DurationMs on enabled exit and exception events.</summary>
        /// <remarks>Measures elapsed time, including asynchronous waits and child calls, excluding entry and completion logging.</remarks>
        /// <value><see langword="false"/> by default.</value>
        public bool LogMethodDuration { get; set; } = false;

        /// <summary>Includes the method name in each event.</summary>
        /// <value><see langword="true"/> by default.</value>
        public bool LogMethodName { get; set; } = true;

        /// <summary>Includes the serialized return value on exit and a blank return-value component on exception events.</summary>
        /// <value><see langword="false"/> by default.</value>
        public bool LogMethodReturnValue { get; set; }

        /// <summary>Includes the current W3C activity's 16-character hexadecimal span ID as LogThisSpanId.</summary>
        /// <remarks>Reads existing activity context per eligible event without creating spans. Independent of LogTraceId; absent, non-W3C, or invalid context is omitted.</remarks>
        /// <value><see langword="false"/> by default.</value>
        public bool LogSpanId { get; set; } = false;

        /// <summary>Includes the current W3C activity's 32-character hexadecimal trace ID as LogThisTraceId.</summary>
        /// <remarks>Reads existing activity context per eligible event without creating tracing. Independent of LogSpanId; absent, non-W3C, or invalid context is omitted.</remarks>
        /// <value><see langword="false"/> by default.</value>
        public bool LogTraceId { get; set; } = false;

        /// <summary>Additional structured properties included with every event.</summary>
        /// <remarks>
        /// Names are validated at configuration resolution and copied when the scope factory is created.
        /// Later dictionary edits no longer affect that factory's events; referenced values are not cloned.
        /// Reserved names (case-insensitive, with or without braces) are LogThisOnEntry, LogThisOnExit,
        /// LogThisOnException, LogThisClass, LogThisMethod, LogThisArguments, LogThisReturnValue,
        /// LogThisDurationMs, LogThisCorrelationId, LogThisTraceId, and LogThisSpanId, even when the corresponding feature is disabled.
        /// </remarks>
        /// <value>An initially empty dictionary whose keys are message-template placeholders.</value>
        public Dictionary<string, object> MessageComponents { get; } = [];

        /// <summary>Text separating placeholders in the message template.</summary>
        /// <value><c> | </c> by default.</value>
        public string MessageDelimeter { get; set; } = Constants.MessageComponents.DefaultDelimeter;

        /// <summary>Settings for method-entry events.</summary>
        /// <value>Entry events are enabled at Information level by default.</value>
        public IAccessPointConfiguration OnEntryConfig { get; set; } = new OnEntryConfiguration();

        /// <summary>Settings for exception events.</summary>
        /// <value>Exception events are enabled at Error level by default.</value>
        public IAccessPointConfiguration OnExceptionConfig { get; set; } = new OnExceptionConfiguration();

        /// <summary>Settings for successful method-exit events.</summary>
        /// <value>Exit events are enabled at Information level by default.</value>
        public IAccessPointConfiguration OnExitConfig { get; set; } = new OnExitConfiguration();

        #endregion

        #region Public Methods

        /// <summary>Adds custom structured properties, wrapping names as message-template placeholders.</summary>
        /// <param name="messageComponents">Property names and values to add to every event.</param>
        /// <remarks>Keys are normalized before insertion into <see cref="MessageComponents"/>.</remarks>
        /// <exception cref="ArgumentException">A name is reserved, malformed, or already present after normalization.</exception>
        public void AddMessageComponents(Dictionary<string, object> messageComponents)
        {
            foreach (KeyValuePair<string, object> component in messageComponents)
            {
                MessageComponents.Add(component.Key.PrepComponentName(), component.Value);
            }
        }

        #endregion
    }
}
