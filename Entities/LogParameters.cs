namespace LogThis.Entities
{
    /// <summary>Builds a logging template and its positional arguments in matching order.</summary>
    /// <param name="messageDelimiter">Separator placed between message fragments.</param>
    public class LogParameters(string messageDelimiter)
    {
        #region Private Properties

        // Values correspond positionally to the placeholders in _messages.
        private readonly List<object> _args = [];

        // The delimiter is applied only when the final template is requested.
        private readonly List<string> _messages = [];

        #endregion

        #region Public Properties

        /// <summary>Gets a snapshot of values for the structured logging placeholders.</summary>
        /// <value>A new array containing the values added so far.</value>
        public object[] Args => [.. _args];

        /// <summary>The complete delimited message template.</summary>
        /// <value>Message fragments joined with the configured delimiter.</value>
        public string Message => string.Join(messageDelimiter, _messages);

        #endregion

        #region Public Methods

        /// <summary>Appends a value for the next message placeholder.</summary>
        /// <param name="obj">Value to associate with a placeholder.</param>
        public void AddArgs(object obj)
        {
            _args.Add(obj);
        }

        /// <summary>Appends a message fragment or structured placeholder.</summary>
        /// <param name="message">Fragment to append to the template.</param>
        public void AddMessage(string message)
        {
            _messages.Add(message);
        }

        /// <summary>Appends custom placeholders and their values in matching order.</summary>
        /// <param name="messageComponents">Structured placeholders mapped to values.</param>
        public void AddMessageComponents(Dictionary<string, object> messageComponents)
        {
            // Preserve matching placeholder/value order for custom components.
            _messages.AddRange([.. messageComponents.Keys]);
            _args.AddRange([.. messageComponents.Values]);
        }

        #endregion
    }
}
