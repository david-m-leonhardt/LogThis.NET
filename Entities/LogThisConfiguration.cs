using LogThis.Constants;
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
        /// <value>An initially empty, mutable list.</value>
        public List<string> JsonFieldsToMask { get; set; } = [];

        /// <summary>Replacement text for each masked JSON field.</summary>
        /// <value><c>*****</c> by default.</value>
        public string JsonMaskValue { get; set; } = MessageComponentConstants.DefaultJsonMaskValue;

        /// <summary>Includes the declaring class name in each event.</summary>
        /// <value><see langword="false"/> by default.</value>
        public bool LogClassName { get; set; } = false;

        /// <summary>Includes friendly component labels in message templates without changing structured values.</summary>
        /// <value><see langword="true"/> by default.</value>
        public bool LogComponentLabels { get; set; } = true;

        /// <summary>Creates a correlation ID when the outermost LogThis scope starts.</summary>
        /// <remarks>The ID has 16 case-sensitive alphanumeric characters and flows across awaits. Nested scopes inherit the ID or its absence. Generation failure leaves the scope without an ID. Only LogThis events are enriched.</remarks>
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

        /// <summary>Additional structured properties included with every event.</summary>
        /// <remarks>
        /// Names are validated at configuration resolution and copied when the scope factory is created.
        /// Later dictionary edits no longer affect that factory's events; referenced values are not cloned.
        /// Reserved names (case-insensitive, with or without braces) are LogThisOnEntry, LogThisOnExit,
        /// LogThisOnException, LogThisClass, LogThisMethod, LogThisArguments, LogThisReturnValue,
        /// LogThisDurationMs, and LogThisCorrelationId, even when the corresponding feature is disabled.
        /// </remarks>
        /// <value>An initially empty dictionary whose keys are message-template placeholders.</value>
        public Dictionary<string, object> MessageComponents { get; } = [];

        /// <summary>Text separating placeholders in the message template.</summary>
        /// <value><c> | </c> by default.</value>
        public string MessageDelimeter { get; set; } = MessageComponentConstants.DefaultDelimeter;

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
