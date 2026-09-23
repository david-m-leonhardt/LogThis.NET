namespace LogThis.Constants
{
    /// <summary>Structured logging placeholders and default formatting values.</summary>
    public static class MessageComponentConstants
    {
        /// <summary>Placeholder for serialized method arguments.</summary>
        public const string ArgumentsLabel = "{Arguments}";
        /// <summary>Placeholder for the declaring class name.</summary>
        public const string ClassLabel = "{Class}";
        /// <summary>Default logging category for manually created scopes.</summary>
        public const string DefaultCategoryName = "LogThis";
        /// <summary>Default separator between message-template components.</summary>
        public const string DefaultDelimeter = " | ";
        /// <summary>Default replacement text for masked JSON fields.</summary>
        public const string DefaultJsonMaskValue = "*****";
        /// <summary>Placeholder for the method name.</summary>
        public const string MethodNameLabel = "{Method}";
        /// <summary>Placeholder for the entry-event text.</summary>
        public const string OnEntryLabel = "{OnEntry}";
        /// <summary>Placeholder for the exception-event text.</summary>
        public const string OnExceptionLabel = "{OnException}";
        /// <summary>Placeholder for the exit-event text.</summary>
        public const string OnExitLabel = "{OnExit}";
        /// <summary>Placeholder for the serialized return value.</summary>
        public const string ReturnValueLabel = "{ReturnValue}";
    }
}
