using LogThis.Constants;
using LogThis.Interfaces;

namespace LogThis.Entities
{
    /// <summary>Builds a logging template and its positional arguments in matching order.</summary>
    public class LogParameters
    {
        #region Private Properties

        /// <summary>Component-label preference captured when the event template is constructed.</summary>
        private readonly bool hasComponentLabels;

        /// <summary>Selected delimiter used to join this event's message fragments.</summary>
        private readonly string messageDelimiter;

        // Values correspond positionally to the placeholders in _messages.
        private readonly List<object> _args = [];

        // The delimiter is applied only when the final template is requested.
        private readonly List<string> _messages = [];

        #endregion

        #region Constructors

        /// <summary>Builds enabled library components and appends the runtime's prepared custom components.</summary>
        /// <param name="runtimeValues">Event data and runtime configuration after eligibility checks.</param>
        internal LogParameters(LogThisRuntimeValues runtimeValues)
        {
            hasComponentLabels = runtimeValues.Runtime.Configuration.LogComponentLabels;
            messageDelimiter = runtimeValues.Runtime.Configuration.MessageDelimeter;

            ConfigParamFactory.BuildConfigParameters(runtimeValues).ForEach(Add);

            AddMessageComponents(runtimeValues.Runtime.MessageComponents);
        }

        #endregion

        #region Internal Methods

        /// <summary>Normalizes and validates a detached copy of custom components before logging starts.</summary>
        /// <param name="components">Configured custom names and values.</param>
        /// <returns>A snapshot safe from subsequent dictionary edits.</returns>
        internal static Dictionary<string, object> PrepareComponents(Dictionary<string, object> components)
        {
            Dictionary<string, object> prepared = [];
            foreach (KeyValuePair<string, object> component in components)
                prepared.Add(Extensions.StringExtensions.PrepComponentName(component.Key), component.Value);
            return prepared;
        }

        #endregion

        #region Public Properties

        /// <summary>Gets a snapshot of values for the structured logging placeholders.</summary>
        /// <value>A new array containing the values added so far.</value>
        public object[] Args => [.. _args];

        /// <summary>The complete delimited message template.</summary>
        /// <value>Message fragments joined with the configured delimiter.</value>
        public string Message => string.Join(messageDelimiter, _messages);

        #endregion

        #region Private Methods

        /// <summary>Appends an eligible component's placeholder and value together to preserve their alignment.</summary>
        /// <param name="configParam">Prepared component and its inclusion decision.</param>
        internal void Add(IConfigParameter configParam)
        {
            if (configParam.ShouldAddValue)
            {
                _messages.Add(FormatComponentWithLabel(configParam.Message));
                _args.Add(configParam.Obj!);
            }
        }

        /// <summary>Appends custom placeholders and their values in matching order.</summary>
        /// <param name="messageComponents">Structured placeholders mapped to values.</param>
        private void AddMessageComponents(Dictionary<string, object> messageComponents)
        {
            // Preserve matching placeholder/value order for custom components.
            _messages.AddRange([.. messageComponents.Keys]);
            _args.AddRange([.. messageComponents.Values]);
        }

        /// <summary>Prefixes recognized placeholders with a friendly label when labels are enabled.</summary>
        private string FormatComponentWithLabel(string componentLabel)
        {
            string label = hasComponentLabels &&
                          MessageComponents.LabelMap.TryGetValue(componentLabel, out string? value)
               ? value
               : string.Empty;

            return label + componentLabel;
        }

        #endregion
    }
}
