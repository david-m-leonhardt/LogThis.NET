namespace LogThis.Entities
{
    public class LogParameters(string messageDelimiter)
    {
        #region Private Properties

        private readonly List<object> _args = [];

        private readonly List<string> _messages = [];

        #endregion

        #region Public Properties

        public object[] Args => [.. _args];

        public string Message => string.Join(messageDelimiter, _messages);

        #endregion

        #region Public Methods

        public void AddArgs(object obj)
        {
            _args.Add(obj);
        }

        public void AddMessage(string message)
        {
            _messages.Add(message);
        }

        public void AddMessageComponents(Dictionary<string, object> messageComponents)
        {
            _messages.AddRange([.. messageComponents.Keys]);
            _args.AddRange([.. messageComponents.Values]);
        }

        #endregion
    }
}
