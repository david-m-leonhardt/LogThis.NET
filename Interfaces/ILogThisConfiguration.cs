namespace LogThis.Interfaces
{
    /// <summary>Options shared by the aspect, message builder, and runtime logger.</summary>
    public interface ILogThisConfiguration
    {
        #region Properties

        /// <summary>Whether internal logging failures are reported at Debug level.</summary>
        /// <value><see langword="true"/> to report failures; otherwise <see langword="false"/>.</value>
        bool DebugLogThis { get; set; }

        /// <summary>JSON property names to redact in logged values.</summary>
        /// <value>The field names supplied by the application.</value>
        List<string> JsonFieldsToMask { get; set; }

        /// <summary>Replacement text used for masked JSON fields.</summary>
        /// <value>The text substituted for each matched field value.</value>
        string JsonMaskValue { get; set; }

        /// <summary>Whether to include the declaring class name.</summary>
        /// <value><see langword="true"/> to add a class component.</value>
        bool LogClassName { get; set; }

        /// <summary>Whether to include serialized method arguments.</summary>
        /// <value><see langword="true"/> to add arguments to entry and exception events.</value>
        bool LogMethodArguments { get; set; }

        /// <summary>Whether to include the method name.</summary>
        /// <value><see langword="true"/> to add a method-name component.</value>
        bool LogMethodName { get; set; }

        /// <summary>Whether to include the serialized return value on exit.</summary>
        /// <remarks>Exception events currently include an empty return-value component when enabled.</remarks>
        /// <value><see langword="true"/> to add a return-value component.</value>
        bool LogMethodReturnValue { get; set; }

        /// <summary>Custom properties added to every log event.</summary>
        /// <value>Message-template placeholders mapped to their values.</value>
        Dictionary<string, object> MessageComponents { get; }

        /// <summary>Separator between message-template components.</summary>
        /// <value>The text placed between adjacent components.</value>
        string MessageDelimeter { get; set; }

        /// <summary>Entry-event settings.</summary>
        /// <value>Message, enabled state, and severity for method entry.</value>
        IAccessPointConfiguration OnEntryConfig { get; set; }

        /// <summary>Exception-event settings.</summary>
        /// <value>Message, enabled state, and severity for method exceptions.</value>
        IAccessPointConfiguration OnExceptionConfig { get; set; }

        /// <summary>Exit-event settings.</summary>
        /// <value>Message, enabled state, and severity for successful method exit.</value>
        IAccessPointConfiguration OnExitConfig { get; set; }

        #endregion

        #region Methods

        /// <summary>Adds custom structured properties to every event.</summary>
        /// <param name="messageComponents">Property names and values to register.</param>
        /// <remarks>Implementations can normalize keys into message-template placeholders.</remarks>
        /// <exception cref="ArgumentException">A normalized property name is already present.</exception>
        void AddMessageComponents(Dictionary<string, object> messageComponents);

        #endregion
    }
}
