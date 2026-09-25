namespace LogThis.Extensions
{
    /// <summary>Helpers for message-template component names.</summary>
    public static class StringExtensions
    {
        #region Extension Block
        // String extension members support custom message components.
        extension(string source)
        {
            /// <summary>Wraps a custom component name in braces for a logging template.</summary>
            /// <returns>The receiver formatted as a message-template placeholder.</returns>
            public string PrepComponentName()
            {
                if (source.Contains('{') && source[0] != '{')
                {
                    source = ReplaceEscapedBracket(source, "{");
                }
                if (source.Contains('}') && source[^1] != '}')
                {
                    source = ReplaceEscapedBracket(source, "}");
                }

                if (!source.StartsWith('{'))
                {
                    source = "{" + source;
                }
                if (!source.EndsWith('}'))
                {
                    source += "}";
                }

                return source;
            }
        }

        #endregion

        #region Private Method

        /// <summary>Escapes a brace embedded in a custom component name.</summary>
        /// <param name="name">Component name to inspect.</param>
        /// <param name="str">Brace character to escape.</param>
        /// <returns>The component name with the selected brace escaped.</returns>
        private static string ReplaceEscapedBracket(string name, string str)
        {
            return name.Replace(str, $"\\\\{str}");
        }

        #endregion
    }
}
